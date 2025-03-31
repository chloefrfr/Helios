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
            MigrateTables();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open database connection: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Migrates tables based on the registered entities.
    /// </summary>
    private void MigrateTables()
    {
        var entityTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttributes(typeof(EntityAttribute), true).Any());

        foreach (var entityType in entityTypes)
        {
            MigrateTable(entityType);
        }
    }

    /// <summary>
    /// Migrates a table based on the specified entity type.
    /// Creates the table if it doesn't exist or updates it if the schema has changed.
    /// </summary>
    /// <param name="entityType">The type of the entity to migrate.</param>
    private void MigrateTable(Type entityType)
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
        else
        {
            UpdateTableIfChanged(entityType);
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

        bool needsTableRebuild = false;

        foreach (var column in entityColumns)
        {
            if (existingColumns.ContainsKey(column.Key) && existingColumns[column.Key] != column.Value)
            {
                if (!CanAlterColumnTypeInPlace(existingColumns[column.Key], column.Value))
                {
                    needsTableRebuild = true;
                    break;
                }
            }
        }

        if (needsTableRebuild)
        {
            RebuildTableWithData(entityType, tableName, existingColumns, entityColumns);
        }
        else
        {
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
                if (!entityColumns.ContainsKey(column.Key) && column.Key.ToLower() != "id")
                {
                    DropColumn(tableName, column.Key);
                }
            }
        }

        Logger.Info($"Table '{tableName}' has been successfully migrated to match the entity definition.");
    }

    /// <summary>
    /// Determines if a column type can be altered in place without data loss.
    /// </summary>
    /// <param name="existingType">The existing PostgreSQL column type.</param>
    /// <param name="newType">The new PostgreSQL column type.</param>
    /// <returns>True if the type can be altered in place; otherwise, false.</returns>
    private bool CanAlterColumnTypeInPlace(string existingType, string newType)
    {
        var safeConversions = new Dictionary<string, List<string>>
        {
            { "smallint", new List<string> { "integer", "bigint", "decimal", "numeric" } },
            { "integer", new List<string> { "bigint", "decimal", "numeric" } },
            { "character varying", new List<string> { "text" } },
            { "varchar", new List<string> { "text" } },
            { "real", new List<string> { "double precision" } },
        };

        existingType = existingType.ToLower();
        newType = newType.ToLower();

        if (existingType == newType)
            return true;

        if (safeConversions.ContainsKey(existingType) && safeConversions[existingType].Contains(newType))
            return true;

        return false;
    }

    /// <summary>
    /// Rebuilds a table with its data when schema changes cannot be done in place.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="existingColumns">The existing columns in the table.</param>
    /// <param name="entityColumns">The columns defined in the entity.</param>
    private void RebuildTableWithData(Type entityType, string tableName, Dictionary<string, string> existingColumns, Dictionary<string, string> entityColumns)
    {
        Logger.Info($"Performing full table rebuild for '{tableName}' due to incompatible schema changes.");

        string tempTableName = $"{tableName}_temp_{DateTime.Now.Ticks}";

        var columns = entityType.GetProperties()
            .Where(p => p.Name != "Id")
            .Select(p => $"{p.GetCustomAttribute<ColumnAttribute>()?.ColumnName ?? p.Name} {GetPostgresType(p.PropertyType)}");

        var primaryKey = "Id SERIAL PRIMARY KEY";
        var createSql = $"CREATE TABLE {tempTableName} ({primaryKey}, {string.Join(", ", columns)});";

        try
        {
            using (var transaction = _connection.BeginTransaction())
            {
                // Create temp table
                using (var command = new NpgsqlCommand(createSql, _connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                // Identify columns that exist in both tables
                var commonColumns = existingColumns.Keys
                    .Where(c => entityColumns.ContainsKey(c) || c.ToLower() == "id")
                    .ToList();

                // Copy data from old table to new table
                var copyColumns = string.Join(", ", commonColumns);
                var copySql = $"INSERT INTO {tempTableName} (id, {copyColumns}) SELECT id, {copyColumns} FROM {tableName};";

                using (var command = new NpgsqlCommand(copySql, _connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                // Drop the old table
                var dropSql = $"DROP TABLE {tableName};";
                using (var command = new NpgsqlCommand(dropSql, _connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                // Rename the new table to the original table name
                var renameSql = $"ALTER TABLE {tempTableName} RENAME TO {tableName};";
                using (var command = new NpgsqlCommand(renameSql, _connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                var sequenceSql = $"SELECT setval(pg_get_serial_sequence('{tableName}', 'id'), (SELECT MAX(id) FROM {tableName}));";
                using (var command = new NpgsqlCommand(sequenceSql, _connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
                Logger.Info($"Successfully rebuilt table '{tableName}' with migrated data.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during table rebuild for '{tableName}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the existing columns and their types for a specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>A dictionary of column names and their types.</returns>
    private Dictionary<string, string> GetExistingColumnsWithTypes(string tableName)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    
        foreach (var prop in entityType.GetProperties().Where(p => p.Name.ToLower() != "id"))
        {
            var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.ColumnName ?? prop.Name;
            columns[columnName] = GetPostgresType(prop.PropertyType);
        }
    
        return columns;
    }

    /// <summary>
    /// Compares the existing table schema with the entity schema to determine if they are equal.
    /// </summary>
    /// <param name="existingColumns">The existing columns in the table.</param>
    /// <param name="entityColumns">The columns defined in the entity.</param>
    /// <returns>True if the schemas are equal; otherwise, false.</returns>
    private bool AreSchemasEqual(Dictionary<string, string> existingColumns, Dictionary<string, string> entityColumns)
    {
        var existingDict = new Dictionary<string, string>(existingColumns, StringComparer.OrdinalIgnoreCase);
        var entityDict = new Dictionary<string, string>(entityColumns, StringComparer.OrdinalIgnoreCase);
    
        foreach (var col in entityDict)
        {
            if (!existingDict.ContainsKey(col.Key) || 
                !existingDict[col.Key].Equals(col.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        foreach (var col in existingDict)
        {
            if (col.Key.ToLower() != "id" && !entityDict.ContainsKey(col.Key))
            {
                return false;
            }
        }

        return true;
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
        var sql = $"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = @tableName AND LOWER(column_name) = LOWER(@columnName));";
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
        var sql = $"ALTER TABLE {tableName} ALTER COLUMN {columnName} TYPE {newType} USING {columnName}::{newType};";
        try
        {
            ExecuteNonQuery(sql);
            Logger.Info($"Altered column '{columnName}' in table '{tableName}' to type '{newType}'.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to alter column type: {ex.Message}");
            throw;
        }
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