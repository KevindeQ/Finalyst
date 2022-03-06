using Dapper;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace DbDeploy;

public interface IDbContext
{
    IEnumerable<ModelType> LoadModel<ModelType, ParamType>(string query, ParamType parameters);
    void SaveModel<ModelType>(string query, ModelType model);
    void ApplyQuery(string query);

    string GetDatabaseName();
}

public class DbContext : IDbContext
{
    private readonly string _connectionString;

    public DbContext(string connectionString, bool ignoreInitialCatalog)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        if (ignoreInitialCatalog)
        {
            connectionStringBuilder.InitialCatalog = String.Empty;
        }

        _connectionString = connectionStringBuilder.ConnectionString;
    }

    public IEnumerable<ModelType> LoadModel<ModelType, ParamType>(string query, ParamType parameters)
    {
        if (string.IsNullOrEmpty(query))
            throw new ArgumentException($"{ nameof(query) } can't be null or empty.");

        using IDbConnection connection = new SqlConnection(_connectionString);
        return connection.Query<ModelType>(query, parameters);
    }

    public void SaveModel<ModelType>(string query, ModelType model)
    {
        if (string.IsNullOrEmpty(query))
            throw new ArgumentException($"{ nameof(query) } can't be null or empty.");

        ArgumentNullException.ThrowIfNull(model);

        using IDbConnection connection = new SqlConnection(_connectionString);
        connection.Query(query, model);
    }

    public void ApplyQuery(string query)
    {
        using IDbConnection connection = new SqlConnection(_connectionString);
        connection.Query(query);
    }

    public string GetDatabaseName()
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(_connectionString);
        return connectionStringBuilder.InitialCatalog;
    }
}
