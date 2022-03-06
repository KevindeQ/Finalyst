using System.Text;

namespace DbDeploy.Data;

public interface ISqlDataType
{
    string AsSqlText { get; }
}

public class SqlBigInt : ISqlDataType
{
    public string AsSqlText
    {
        get => "bigint";
    }
}

public class SqlInt : ISqlDataType
{
    public string AsSqlText
    {
        get => "int";
    }
}

public class SqlSmallInt : ISqlDataType
{
    public string AsSqlText
    {
        get => "smallint";
    }
}

public class SqlDate : ISqlDataType
{
    public string AsSqlText
    {
        get => "date";
    }
}

public class SqlDateTime : ISqlDataType
{
    public string AsSqlText
    {
        get => "datetime";
    }
}

public class SqlSmallDateTime : ISqlDataType
{
    public string AsSqlText
    {
        get => "smalldatetime";
    }
}

public class SqlNChar : ISqlDataType
{
    private readonly uint _length;

    public SqlNChar(uint length)
    {
        _length = length;
    }

    public string AsSqlText
    {
        get => $"nchar({ _length.ToString() })";
    }
}

public class SqlNVarChar : ISqlDataType
{
    private readonly uint _length;

    public SqlNVarChar(uint length)
    {
        _length = length;
    }

    public string AsSqlText
    {
        get => $"nvarchar({ _length.ToString() })";
    }
}

public class SqlBinary : ISqlDataType
{
    private readonly uint _length;

    public SqlBinary(uint length)
    {
        _length = length;
    }

    public string AsSqlText
    {
        get => $"binary({ _length.ToString() })";
    }
}

public class SqlVarBinary : ISqlDataType
{
    private readonly uint _length;

    public SqlVarBinary(uint length)
    {
        _length = length;
    }

    public string AsSqlText
    {
        get => $"varbinary({ _length.ToString() })";
    }
}

public class ColumnDefinition
{
    private readonly string _columnName;
    private ISqlDataType? _dataType;
    private bool _isNullable;
    private bool _useInPrimaryKey;

    public ColumnDefinition(string columnName)
    {
        _columnName = columnName;
    }

    public string ColumnName { get { return _columnName; } }
    public ISqlDataType? DataType { get { return _dataType; } }
    public bool IsNullable { get { return _isNullable; } }
    public bool InPrimaryKey
    {
        get { return _useInPrimaryKey; }
        set { _useInPrimaryKey = value; }
    }

    public ColumnDefinition AsBigInt()
    {
        _dataType = new SqlBigInt();
        return this;
    }

    public ColumnDefinition AsInt()
    {
        _dataType = new SqlInt();
        return this;
    }

    public ColumnDefinition AsSmallInt()
    {
        _dataType = new SqlSmallInt();
        return this;
    }

    public ColumnDefinition AsDate()
    {
        _dataType = new SqlDate();
        return this;
    }

    public ColumnDefinition AsDateTime()
    {
        _dataType = new SqlDateTime();
        return this;
    }

    public ColumnDefinition AsSmallDateTime()
    {
        _dataType = new SqlSmallDateTime();
        return this;
    }

    public ColumnDefinition AsNChar(uint length)
    {
        _dataType = new SqlNChar(length);
        return this;
    }

    public ColumnDefinition AsNVarChar(uint length)
    {
        _dataType = new SqlNVarChar(length);
        return this;
    }

    public ColumnDefinition AsBinary(uint length)
    {
        _dataType = new SqlBinary(length);
        return this;
    }

    public ColumnDefinition AsVarBinary(uint length)
    {
        _dataType = new SqlVarBinary(length);
        return this;
    }

    public ColumnDefinition AllowNull()
    {
        _isNullable = true;
        return this;
    }

    public ColumnDefinition UseInPrimaryKey()
    {
        _useInPrimaryKey = true;
        return this;
    }
}

public class TableDefinition
{
    private readonly string _tableName;
    private readonly IList<ColumnDefinition> _columns;

    public TableDefinition(string tableName)
    {
        _tableName = tableName;
        _columns = new List<ColumnDefinition>();
    }

    public string TableName { get { return _tableName; } }
    public IList<ColumnDefinition> Columns { get { return _columns; } }

    public ColumnDefinition WithColumn(string columnName)
    {
        var newColumn = new ColumnDefinition(columnName);
        _columns.Add(newColumn);

        return newColumn;
    }
}

public interface IDataStore
{
    bool DatabaseExists(string databaseName);
    void CreateDatabase(string databaseName);
    bool TableExists(string tableName);
    void CreateTable(TableDefinition tableDefinition);
}

public class DataStore : IDataStore
{
    private readonly IDbContext _dbContext;

    public DataStore(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool DatabaseExists(string databaseName)
    {
        if (string.IsNullOrEmpty(databaseName))
        {
            throw new ArgumentException($"'{ nameof(databaseName) }' can't be null or empty.");
        }

        var dbCount = _dbContext.LoadModel<int, dynamic>(
            "SELECT 1 FROM sys.databases WHERE Name = @DatabaseName", new { databaseName });

        return dbCount.Count() == 1;
    }

    public void CreateDatabase(string databaseName)
    {
        var statement = $"CREATE DATABASE { databaseName }";
        _dbContext.ApplyQuery(statement);
    }

    public bool TableExists(string tableName)
    {
        var tableCount = _dbContext.LoadModel<int, dynamic>(
            "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName", new { tableName });
        return tableCount.Any();
    }

    public void CreateTable(TableDefinition tableDefinition)
    {
        var statement = BuildCreateTableStatement(tableDefinition);
        _dbContext.ApplyQuery(statement);

        statement = BuildCreatePrimaryKeyStatement(tableDefinition);
        if (!string.IsNullOrEmpty(statement))
        {
            _dbContext.ApplyQuery(statement);
        }
    }

    private string BuildCreateTableStatement(TableDefinition tableDefinition)
    {
        var sqlBuilder = new StringBuilder();
        sqlBuilder.Append($"CREATE TABLE { tableDefinition.TableName }(");

        foreach (var column in tableDefinition.Columns)
        {
            sqlBuilder.Append(column.ColumnName);

            if (column.DataType != null)
            {
                sqlBuilder.Append(' ' + column.DataType.AsSqlText);
            }
            else
            {
                throw new Exception(
                    $"Invalid column definition on column '{ column.ColumnName }'. Missing the datatype definition.");
            }

            if (column.IsNullable)
            {
                sqlBuilder.Append(" NULL");
            }
            else
            {
                sqlBuilder.Append(" NOT NULL");
            }

            sqlBuilder.AppendLine(",");
        }

        sqlBuilder.Append($")");

        return sqlBuilder.ToString();
    }

    private string BuildCreatePrimaryKeyStatement(TableDefinition tableDefinition)
    {
        var sqlBuilder = new StringBuilder();

        sqlBuilder.Append($"ALTER TABLE { tableDefinition.TableName } ");
        sqlBuilder.Append($"ADD CONSTRAINT pk_{ tableDefinition.TableName.ToLower() } PRIMARY KEY CLUSTERED (");

        bool isFirstColumn = true;
        foreach (var column in tableDefinition.Columns)
        {
            if (column.InPrimaryKey)
            {
                if (!isFirstColumn)
                {
                    sqlBuilder.Append(", ");
                }

                sqlBuilder.Append(column.ColumnName);
                isFirstColumn = false;
            }
        }

        sqlBuilder.Append(")");

        return sqlBuilder.ToString();
    }
}
