namespace DbDeploy.Data;

public enum MsSqlServerVersion
{
    Unknown,
    Version9,
    Version10,
    Version11,
    Version12,
    Version13,
    Version14,
    Version15
}

public interface IMSSqlServer
{
    MsSqlServerVersion DetermineSqlServerVersion();
}

public class MSSqlServer : IMSSqlServer
{
    private readonly IDbContext _dbContext;

    public MSSqlServer(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public MsSqlServerVersion DetermineSqlServerVersion()
    {
        var sqlServerVerion = _dbContext.LoadModel<int, dynamic>("select SERVERPROPERTY('ProductMajorVersion')", new { });
        if (!sqlServerVerion.Any())
            return MsSqlServerVersion.Unknown;

        switch (sqlServerVerion.First())
        {
            case 9: return MsSqlServerVersion.Version9;
            case 10: return MsSqlServerVersion.Version10;
            case 11: return MsSqlServerVersion.Version11;
            case 12: return MsSqlServerVersion.Version12;
            case 13: return MsSqlServerVersion.Version13;
            case 14: return MsSqlServerVersion.Version14;
            case 15: return MsSqlServerVersion.Version15;

            default: return MsSqlServerVersion.Unknown;
        }
    }
}
