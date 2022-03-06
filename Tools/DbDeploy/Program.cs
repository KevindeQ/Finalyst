using DbDeploy;
using DbDeploy.Data;
using DbDeploy.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Transactions;

await BuildCommandLine()
    .UseHost(
        _ => Host.CreateDefaultBuilder(),
        host =>
        {
            host.ConfigureServices(services =>
            {
                services.AddTransient<IScriptApplicator, ScriptApplicator>();

                services.AddTransient(provider =>
                {
                    return new Func<string, bool, IDbContext>(
                        (connectionString, ignoreInitialCatalog) => new DbContext(connectionString, ignoreInitialCatalog));
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
            });
        })
    .UseDefaults()
    .Build()
    .InvokeAsync(args);

static CommandLineBuilder BuildCommandLine()
{
    var migrateCommand = new Command("migrate", "Migrate the database to a new version")
    {
        new Argument<string>("search_path", "Path to the folder containing the migration scripts"),
        new Argument<string>("connection_string", "Connection string of the database to which to apply the migration scripts"),
        new Option<int>(new[] {"--lower_id", "-l" }, description: "Lower bound of the script id to be executed", getDefaultValue: () => 0),
        new Option<int>(new[] {"--upper_id", "-h" }, description: "Upper bound of the script id to be executed", getDefaultValue: () => 0)
    };
    migrateCommand.Handler = CommandHandler.Create<CommandLineOptions, IHost>(RunMigration);

    var rootCommand = new RootCommand(@"dbdeploy migrate <path to migration scripts>")
    {
        migrateCommand
    };

    return new CommandLineBuilder(rootCommand);
}

const string MigrationTableName = "migration";

static int RunMigration(CommandLineOptions options, IHost host)
{
    var logger = host.Services.GetRequiredService<ILogger<CommandLineOptions>>();

    try
    {
        Func<string, bool, IDbContext> dbContextFactory = host.Services.GetRequiredService<Func<string, bool, IDbContext>>();

        // Create an instance of dbContext ignoring the initial catalog to prevent an error about the database being found
        // if it doesn't exist for the DatabaseExists check.
        var tempDbContext = dbContextFactory(options.connection_string, true);
        IDataStore dataStore = new DataStore(tempDbContext);

        // Create a new instance of dbContext so it can be used to extract the intended database name from
        // the connection string. This instance is also used for further processing of sql scripts.
        var dbContext = dbContextFactory(options.connection_string, false);
        var databaseName = dbContext.GetDatabaseName();

        // Steps to apply database migrations scripts to configured database:
        //   1) Check if db migration table is present
        if (!dataStore.DatabaseExists(databaseName))
        {
            logger.LogInformation("Database doesn't exists. Attempting to create it.");


            if (CreateDatabase(databaseName, dataStore, logger))
            {
                logger.LogInformation("Successfully created database.");
            }
            else
            {
                logger.LogError("Failed to create database. Stopping execution.");
            }
        }
        else
        {
            // Create a new instance of dbContext with the database name embedded in the connection string
            dbContext = dbContextFactory(options.connection_string, false);
        }
        
        // Create a new instance of DataStore using a dbContext that has the database name embedded in the connection
        // string.
        dataStore = new DataStore(dbContext);

        if (!dataStore.TableExists(MigrationTableName))
        {
            logger.LogInformation($"Table '{ MigrationTableName }' doesn't exist. Attempting to create it.");

            if (CreateMigrationTable(dataStore, logger))
            {
                logger.LogInformation($"Successfully created table '{ MigrationTableName }'.");
            }
            else
            {
                logger.LogError($"Failed to create table '{ MigrationTableName }'. Stopping execution.");
                return 1;
            }
        }

        var migrationScriptSearchPath = options.search_path;
        var files = Directory.GetFiles(migrationScriptSearchPath, "*.sql", SearchOption.TopDirectoryOnly);

        // 2) Gather list of all migration scripts
        var migrationScripts = files
            .Select(filename => { return MapToMigrationScript(filename); })
            .Where(migrationScript => { return (migrationScript != null) && FilterMigrationScript(migrationScript, MigrationOp.Migrate, options); });

        IMigrations migrations = new Migrations(dbContext);

        // 3) For each script
        foreach (var migrationScript in migrationScripts)
        {
            if (migrationScript == null)
            {
                continue;
            }

            // a) Start transaction
            using (var transactionScope = new TransactionScope())
            {
                // b) Check if script was already applied to the databse in an earlier migration run
                // c) If migration was already applied, go to next script
                var existingScript = migrations.FindByFilename(migrationScript.Filename);

                if (!existingScript.Equals(migrationScript))
                {
                    // d) Parse all fragments from script
                    //    See Microsoft.SqlServer.TransactSql.ScriptDom to get all the fragments in an sql script. The idea is this parses out any go commands.
                    // e) For each fragment, execute against the database
                    IMSSqlServer sqlServer = new MSSqlServer(dbContext);
                    IScriptApplicator scriptApplicator = new ScriptApplicator(sqlServer, migrations);

                    scriptApplicator.ApplyScript(migrationScript);

                    // f) Record application of script in db migration table
                    migrations.InsertMigrationScript(migrationScript);
                }

                // f) On success, commit transaction
                transactionScope.Complete();
            }

            logger.LogInformation($"Currently processing '{ migrationScript.Filename }'");
        }

        logger.LogInformation("Execution successfully finished.");
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "A fatal error occurred. Stopping execution.");
        return 1;
    }
}

static bool CreateDatabase(string databaseName, IDataStore dataStore, ILogger logger)
{
    try
    {
        dataStore.CreateDatabase(databaseName);

        return true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An exception occurred during creation of the database.");
        return false;
    }
}

static bool CreateMigrationTable(IDataStore dataStore, ILogger logger)
{
    try
    {
        var migrationTableDefinition = new TableDefinition(MigrationTableName);
        migrationTableDefinition
            .WithColumn("migration_id")
            .AsInt()
            .UseInPrimaryKey();
        migrationTableDefinition
            .WithColumn("operation")
            .AsSmallInt();
        migrationTableDefinition
            .WithColumn("description")
            .AsNVarChar(1024);
        migrationTableDefinition
            .WithColumn("filename")
            .AsNVarChar(256);
        migrationTableDefinition
            .WithColumn("file_checksum")
            .AsBinary(32);

        // a) if db migration table is not present, create it
        dataStore.CreateTable(migrationTableDefinition);

        return true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An exception occurred during creation of the migration table.");
        return false;
    }
}

static MigrationScript? MapToMigrationScript(string filePath)
{
    string pattern = @"(?i)^(?<Operation>[mu])(?<Id>\d+)__(?<Description>[a-z0-9\x20_-]+)\.sql$";
    var matcher = new Regex(pattern);

    var filename = Path.GetFileName(filePath);
    var matches = matcher.Matches(filename);
    if (matches.Count == 1)
    {
        var match = matches[0];
        if (match.Success)
        {
            return new MigrationScript()
            {
                Id = int.Parse(match.Groups["Id"].Value),
                Operation = MigrationOpHelpers.Parse(match.Groups["Operation"].Value),
                Description = match.Groups["Description"].Value,
                FilePath = filePath,
                Filename = filename,
                FileChecksum = GetFileChecksum(filePath)
            };
        }
        else
        {
            return null;
        }
    }
    else
    {
        return null;
    }
}

static byte[] GetFileChecksum(string filename)
{
    using var hashingEngine = SHA256.Create();
    using var stream = File.OpenRead(filename);

    return hashingEngine.ComputeHash(stream);
}

static bool FilterMigrationScript(MigrationScript migrationScript, MigrationOp allowedOperation, CommandLineOptions options)
{
    bool checkLowerBound = options.lower_id > 0;
    bool checkUpperBound = options.upper_id > 0;
    bool checkSequenceRange = checkLowerBound || checkUpperBound;

    return
        // Check if the operation is allowed for current run mode
        (migrationScript.Operation == allowedOperation) &&

        // Check if the script sequance number is bounded. If so, check if the given script sequence
        // number falls within the accepted range.
        (!checkSequenceRange ||
        ((!checkLowerBound || options.lower_id <= migrationScript.Id) &&
        (!checkUpperBound || migrationScript.Id <= options.upper_id)));
}