using DbDeploy.Models;

namespace DbDeploy.Data;

public interface IMigrations
{
    MigrationScript FindByFilename(string filename);
    void InsertMigrationScript(MigrationScript script);
    void ApplyMigrationQuery(string query);
}

public class Migrations : IMigrations
{
    private readonly IDbContext _dbContext;

    public Migrations(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public MigrationScript FindByFilename(string filename)
    {
        var migrationScripts = _dbContext.LoadModel<MigrationScript, dynamic>(
            "SELECT migration_id as id, operation, description, filename, file_checksum as filechecksum FROM migration WHERE filename = @Filename", new { filename });
        
        if (migrationScripts.Any())
        {
            return migrationScripts.First();
        }
        else
        {
            return new MigrationScript();
        }
    }

    public void InsertMigrationScript(MigrationScript script)
    {
        _dbContext.SaveModel(
            "INSERT INTO migration (migration_id, operation, description, filename, file_checksum) " +
            "VALUES (@Id, @Operation, @Description, @Filename, @FileChecksum)", script);
    }

    public void ApplyMigrationQuery(string query)
    {
        _dbContext.ApplyQuery(query);
    }
}
