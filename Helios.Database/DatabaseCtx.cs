using System.Collections.Concurrent;
using System.Reflection;
using Helios.Database.Attributes;
using Npgsql;

namespace Helios.Database;

public class DatabaseCtx : IDisposable
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private NpgsqlConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseCtx"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string to the database.</param>
    public DatabaseCtx(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        try
        {
            _connection = new NpgsqlConnection(_connectionString);
            _connection.Open();
            Logger.Info("Database connection opened.");
            CreateTables();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open database connection: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Creates the necessary tables based on the registered entities.
    /// </summary>
    private void CreateTables()
    {
        var entityTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttributes(typeof(EntityAttribute), true).Any());

        foreach (var entityType in entityTypes)
        {
            CreateOrUpdateTable(entityType);
        }
    }


    /// <summary>
    /// Creates a new table or updates an existing table based on the specified entity type.
    /// </summary>
    /// <param name="entityType">The type of the entity to create or update.</param>
    private void CreateOrUpdateTable(Type entityType)
    {
        var tableName = entityType.GetCustomAttribute<EntityAttribute>()?.TableName;

        if (string.IsNullOrEmpty(tableName))
        {
            Logger.Error($"Entity '{entityType.Name}' does not have a TableAttribute specified.");
            return;
        }

        if (!TableExists(tableName))
        {
            CreateTable(entityType);
        }
    }

    /// <summary>
    /// Updates an existing table only if there are changes in the entity definition.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    private void UpdateTableIfChanged(Type entityType)
    {
        var tableName = entityType.GetCustomAttribute<EntityAttribute>()?.TableName;

        if (string.IsNullOrEmpty(tableName))
        {
            Logger.Error($"Entity '{entityType.Name}' does not have a TableAttribute specified.");
            return;
        }

        var existingColumns = GetExistingColumnsWithTypes(tableName);
        var entityColumns = GetEntityColumnsWithTypes(entityType);

        if (!AreSchemasEqual(existingColumns, entityColumns))
        {
            UpdateTable(entityType, existingColumns, entityColumns);
        }
        else
        {
            Logger.Error($"Table '{tableName}' is up-to-date. No migration needed.");
        }
    }

    /// <summary>
    /// Updates the table schema to match the entity definition.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="existingColumns">The existing columns in the table.</param>
    /// <param name="entityColumns">The columns defined in the entity.</param>
    private void UpdateTable(Type entityType, Dictionary<string, string> existingColumns, Dictionary<string, string> entityColumns)
    {
        var tableName = entityType.GetCustomAttribute<EntityAttribute>()?.TableName;

        if (string.IsNullOrEmpty(tableName))
        {
            Logger.Error($"Entity '{entityType.Name}' does not have a TableAttribute specified.");
            return;
        }

        foreach (var column in entityColumns)
        {
            if (!existingColumns.ContainsKey(column.Key))
            {
                AddColumn(tableName, column.Key, column.Value);
            }
            else if (existingColumns[column.Key] != column.Value)
            {
                AlterColumnType(tableName, column.Key, column.Value);
            }
        }

        foreach (var column in existingColumns)
        {
            if (!entityColumns.ContainsKey(column.Key))
            {
                DropColumn(tableName, column.Key);
            }
        }

        Logger.Info($"Table '{tableName}' has been updated to match the entity definition.");
    }

    /// <summary>
    /// Gets the existing columns and their types for a specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>A dictionary of column names and their types.</returns>
    private Dictionary<string, string> GetExistingColumnsWithTypes(string tableName)
    {
        var columns = new Dictionary<string, string>();
        var sql = $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @tableName;";
        using (var command = new NpgsqlCommand(sql, _connection))
        {
            command.Parameters.AddWithValue("@tableName", tableName);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(reader.GetString(0), reader.GetString(1));
                }
            }
        }
        return columns;
    }

    /// <summary>
    /// Gets the columns and their types for a specified entity.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <returns>A dictionary of column names and their types.</returns>
    private Dictionary<string, string> GetEntityColumnsWithTypes(Type entityType)
    {
        return entityType.GetProperties()
            .Where(p => p.Name != "Id")
            .ToDictionary(
                p => p.GetCustomAttribute<ColumnAttribute>()?.ColumnName ?? p.Name,
                p => GetPostgresType(p.PropertyType)
            );
    }


    /// <summary>
    /// Compares the existing table schema with the entity schema to determine if they are equal.
    /// </summary>
    /// <param name="existingColumns">The existing columns in the table.</param>
    /// <param name="entityColumns">The columns defined in the entity.</param>
    /// <returns>True if the schemas are equal; otherwise, false.</returns>
    private bool AreSchemasEqual(Dictionary<string, string> existingColumns, Dictionary<string, string> entityColumns)
    {
        return existingColumns.Count == entityColumns.Count &&
               existingColumns.All(ec => entityColumns.ContainsKey(ec.Key) && entityColumns[ec.Key] == ec.Value);
    }

    /// <summary>
    /// Adds a new column to an existing table if it does not already exist.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column to add.</param>
    /// <param name="columnType">The PostgreSQL type of the column.</param>
    private void AddColumn(string tableName, string columnName, string columnType)
    {
        if (ColumnExists(tableName, columnName))
        {
            Logger.Warn($"Column '{columnName}' already exists in table '{tableName}'. Skipping add.");
            return;
        }

        var sql = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
        ExecuteNonQuery(sql);
        Logger.Info($"Added column '{columnName}' to table '{tableName}'.");
    }

    /// <summary>
    /// Checks if a column exists in a table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column to check.</param>
    /// <returns>True if the column exists; otherwise, false.</returns>
    private bool ColumnExists(string tableName, string columnName)
    {
        var sql = $"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = @tableName AND column_name = @columnName);";
        using (var command = new NpgsqlCommand(sql, _connection))
        {
            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@columnName", columnName);
            return (bool)command.ExecuteScalar();
        }
    }

    /// <summary>
    /// Alters the type of an existing column.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column to alter.</param>
    /// <param name="newType">The new PostgreSQL type for the column.</param>
    private void AlterColumnType(string tableName, string columnName, string newType)
    {
        var sql = $"ALTER TABLE {tableName} ALTER COLUMN {columnName} TYPE {newType};";
        ExecuteNonQuery(sql);
        Logger.Info($"Altered column '{columnName}' in table '{tableName}' to type '{newType}'.");
    }

    /// <summary>
    /// Drops a column from an existing table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column to drop.</param>
    private void DropColumn(string tableName, string columnName)
    {
        var sql = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
        ExecuteNonQuery(sql);
        Logger.Info($"Dropped column '{columnName}' from table '{tableName}'.");
    }

    /// <summary>
    /// Executes a non-query SQL command.
    /// </summary>
    /// <param name="sql">The SQL command to execute.</param>
    private void ExecuteNonQuery(string sql)
    {
        using (var command = new NpgsqlCommand(sql, _connection))
        {
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Creates a table for the specified entity type if it does not already exist.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    private void CreateTable(Type entityType)
    {
        var tableName = entityType.GetCustomAttribute<EntityAttribute>()?.TableName;

        if (string.IsNullOrEmpty(tableName))
        {
            Logger.Error($"Entity '{entityType.Name}' does not have a TableAttribute specified.");
            return;
        }

        if (TableExists(tableName))
        {
            Logger.Warn($"Table '{tableName}' already exists.");
            return;
        }

        var columns = entityType.GetProperties()
            .Where(p => p.Name != "Id")
            .Select(p => $"{p.GetCustomAttribute<ColumnAttribute>()?.ColumnName ?? p.Name} {GetPostgresType(p.PropertyType)}");

        var primaryKey = "Id SERIAL PRIMARY KEY";
        var sql = $"CREATE TABLE IF NOT EXISTS {tableName} ({primaryKey}, {string.Join(", ", columns)});";

        try
        {
            using (var command = new NpgsqlCommand(sql, _connection))
            {
                command.ExecuteNonQuery();
                Logger.Info($"Table {tableName} created.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error creating table {tableName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="tableName">The name of the table to check.</param>
    /// <returns>True if the table exists; otherwise, false.</returns>
    private bool TableExists(string tableName)
    {
        var sql = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{tableName}');";
        using (var command = new NpgsqlCommand(sql, _connection))
        {
            return (bool)command.ExecuteScalar();
        }
    }

    /// <summary>
    /// Maps C# types to PostgreSQL types.
    /// </summary>
    /// <param name="type">The type to map.</param>
    /// <returns>The corresponding PostgreSQL type as a string.</returns>
    private string GetPostgresType(Type type)
    {
        if (type == typeof(System.Dynamic.ExpandoObject) || type == typeof(object))
        {
            return "TEXT";
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            var postgresType = GetPostgresType(elementType);
            return $"{postgresType}[]";
        }

        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType switch
        {
            Type t when t == typeof(string) => "TEXT",
            Type t when t == typeof(int) => "INTEGER",
            Type t when t == typeof(long) => "BIGINT",
            Type t when t == typeof(double) => "DOUBLE PRECISION",
            Type t when t == typeof(float) => "REAL",
            Type t when t == typeof(bool) => "BOOLEAN",
            Type t when t == typeof(DateTime) => "TIMESTAMP",
            Type t when t == typeof(decimal) => "DECIMAL",
            Type t when t == typeof(short) => "SMALLINT",
            Type t when t == typeof(byte) => "BYTEA",
            Type t when t == typeof(char) => "CHAR",
            Type t when t == typeof(Guid) => "UUID",
            Type t when t == typeof(object) => "JSONB",
            _ => "TEXT" // Fallback for unsupported types.
        };
    }

    /// <summary>
    /// Disposes the database connection and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}