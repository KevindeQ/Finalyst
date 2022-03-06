using DbDeploy.Data;
using DbDeploy.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DbDeploy;

public interface IScriptApplicator
{
    void ApplyScript(MigrationScript script);
}

public class ScriptApplicator : IScriptApplicator
{
    private readonly IMSSqlServer _sqlServer;
    private readonly IMigrations _migrations;

    public ScriptApplicator(IMSSqlServer sqlServer, IMigrations migrations)
    {
        _sqlServer = sqlServer;
        _migrations = migrations;
    }

    public void ApplyScript(MigrationScript script)
    {
        var msSqlServerVersion = _sqlServer.DetermineSqlServerVersion();

        IList<ParseError> parseErrors;

        using (TextReader fileReader = new StreamReader(script.FilePath))
        {
            var parser = GetSqlParser(msSqlServerVersion);
            var fragment = parser.Parse(fileReader, out parseErrors);

            if (parseErrors != null && parseErrors.Count > 0)
            {
                foreach (ParseError error in parseErrors)
                {

                }

                return;
            }

            var sqlScriptGenerator = GetSqlScriptGenerator(msSqlServerVersion);
            var sqlScript = fragment as TSqlScript;
            if (sqlScript != null)
            {
                foreach (var sqlBatch in sqlScript.Batches)
                {
                    string query;
                    sqlScriptGenerator.GenerateScript(sqlBatch, out query);

                    _migrations.ApplyMigrationQuery(query);
                }
            }
            else
            {
                string query;
                sqlScriptGenerator.GenerateScript(fragment, out query);

                _migrations.ApplyMigrationQuery(query);
            }
        }

    }

    private TSqlParser GetSqlParser(MsSqlServerVersion version)
    {
        switch (version)
        {
            case MsSqlServerVersion.Version9: return new TSql90Parser(true);
            case MsSqlServerVersion.Version10: return new TSql110Parser(true);
            case MsSqlServerVersion.Version11: return new TSql120Parser(true);
            case MsSqlServerVersion.Version12: return new TSql130Parser(true);
            case MsSqlServerVersion.Version13: return new TSql140Parser(true);
            case MsSqlServerVersion.Version14: return new TSql150Parser(true);
            case MsSqlServerVersion.Version15: return new TSql160Parser(true);

            default:
                throw new Exception("Unsupported SQL Server version.");
        }
    }

    private SqlScriptGenerator GetSqlScriptGenerator(MsSqlServerVersion version)
    {
        switch (version)
        {
            case MsSqlServerVersion.Version9: return new Sql90ScriptGenerator();
            case MsSqlServerVersion.Version10: return new Sql100ScriptGenerator();
            case MsSqlServerVersion.Version11: return new Sql110ScriptGenerator();
            case MsSqlServerVersion.Version12: return new Sql120ScriptGenerator();
            case MsSqlServerVersion.Version13: return new Sql130ScriptGenerator();
            case MsSqlServerVersion.Version14: return new Sql140ScriptGenerator();
            case MsSqlServerVersion.Version15: return new Sql150ScriptGenerator();

            default:
                throw new Exception("Unsupported SQL Server version.");
        }
    }
}
