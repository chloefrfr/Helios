using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Helios.Database.Attributes;
using Helios.Database.Repository.Json;
using Helios.Database.Repository.String;
using Helios.Database.Tables;
using Helios.Database.Mappings;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

namespace Helios.Database.Repository
{
    public class Repository<TEntity> where TEntity : BaseTable, new()
    {
        private readonly string _connectionString;
        private static readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new();
        private static readonly SemaphoreSlim _bulkLock = new(1, 1);
        private const int MaxBatchSize = 50000;

        public Repository(string connectionString)
        {
            _connectionString = connectionString;
            SqlMapper.AddTypeHandler(new JsonListManager());
            SqlMapper.SetTypeMap(typeof(TEntity), new CustomPropertyTypeMap(typeof(TEntity),
                (type, columnName) => type.GetProperties().FirstOrDefault(prop =>
                    prop.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))));
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<TEntity> FindAsync(TEntity template, int timeout = 500)
        {
            var metadata = GetMetadata();
            using var conn = CreateConnection();
            conn.Open();

            var (whereClause, parameters) = BuildWhereClause(template, metadata);
            var sql = $"SELECT {metadata.AllColumns} FROM {metadata.TableName} {whereClause} LIMIT 1";

            return await conn.QueryFirstOrDefaultAsync<TEntity>(sql, parameters, commandTimeout: timeout);
        }

        public async Task<int> UpdateAsync(TEntity entity)
        {
            var metadata = GetMetadata();
            using var conn = CreateConnection();
            conn.Open();

            var keyProperty = metadata.Properties.FirstOrDefault(p => p.IsKey);
            if (keyProperty == null)
                throw new InvalidOperationException("Entity must have a primary key defined for updates.");

            var nonKeyProperties = metadata.Properties.Where(p => !p.IsKey).ToList();
            var setClause = string.Join(", ", nonKeyProperties.Select(p => $"{p.ColumnName} = @{p.Name}"));

            var sql = $@"
                UPDATE {metadata.TableName}
                SET {setClause}
                WHERE {keyProperty.ColumnName} = @{keyProperty.Name}";

            var parameters = CreateParameters(entity, metadata);

            return await conn.ExecuteAsync(sql, parameters);
        }

        public async Task<int> DeleteAsync(TEntity template)
        {
            var metadata = GetMetadata();
            using var conn = CreateConnection();
            conn.Open();

            var (whereClause, parameters) = BuildWhereClause(template, metadata);

            if (string.IsNullOrEmpty(whereClause))
                throw new InvalidOperationException("No conditions provided for deletion. To delete all records, use DeleteAllAsync.");

            var sql = $"DELETE FROM {metadata.TableName} {whereClause}";
            return await conn.ExecuteAsync(sql, parameters);
        }

        public async Task<IEnumerable<TEntity>> FindAllAsync(TEntity template, int limit = 5000)
        {
            var metadata = GetMetadata();
            using var conn = CreateConnection();
            conn.Open();

            var (whereClause, parameters) = BuildWhereClause(template, metadata);
            var sql = $"SELECT {metadata.AllColumns} FROM {metadata.TableName} {whereClause} LIMIT {limit}";

            return await conn.QueryAsync<TEntity>(sql, parameters);
        }

        public async Task<int> SaveAsync(TEntity entity, bool returnId = true)
        {
            var metadata = GetMetadata();
            using var conn = CreateConnection();
            conn.Open();

            var (columns, values, updates) = GetInsertData(metadata);
            var sql = $@"INSERT INTO {metadata.TableName} ({columns})
                VALUES ({values})
                ON CONFLICT (id) DO UPDATE SET {updates}
                {(returnId ? "RETURNING id" : "")}";

            var parameters = CreateParameters(entity, metadata);

            if (returnId)
            {
                var newId = await conn.ExecuteScalarAsync<int>(sql, parameters);

                var idProperty = metadata.Properties.FirstOrDefault(p => p.IsKey);
                if (idProperty != null)
                {
                    idProperty.PropertyInfo.SetValue(entity, newId);
                }

                return newId;
            }

            return await conn.ExecuteAsync(sql, parameters);
        }
        public async Task BulkInsertAsync(IEnumerable<TEntity> entities)
        {
            var metadata = GetMetadata();
            using var conn = (NpgsqlConnection)CreateConnection();
            await conn.OpenAsync();

            using var writer = conn.BeginBinaryImport(metadata.CopyCommand);
            foreach (var entity in entities)
            {
                writer.StartRow();
                foreach (var prop in metadata.InsertProperties)
                {
                    var value = prop.PropertyInfo.GetValue(entity);
                    WriteValue(writer, value);
                }
            }
            writer.Complete();
        }

        public async Task BulkSaveAsync(IEnumerable<TEntity> entities)
        {
            await _bulkLock.WaitAsync();
            try
            {
                var metadata = GetMetadata();
                using var conn = (NpgsqlConnection)CreateConnection();
                await conn.OpenAsync();

                using var writer = conn.BeginBinaryImport(metadata.CopyCommand);
                foreach (var entity in entities)
                {
                    writer.StartRow();

                    foreach (var prop in metadata.InsertProperties)
                    {
                        WriteValue(writer, prop.PropertyInfo.GetValue(entity));
                    }
                }
                writer.Complete();
            }
            finally
            {
                _bulkLock.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (string, DynamicParameters) BuildWhereClause(TEntity template, EntityMetadata metadata)
        {
            var sb = new StringBuilder("WHERE ");
            var parameters = new DynamicParameters();
            var first = true;

            foreach (var prop in metadata.Properties)
            {
                var value = prop.PropertyInfo.GetValue(template);
                if (IsDefaultValue(value)) continue;

                if (!first) sb.Append(" AND ");
                sb.Append($"{prop.ColumnName} = @{prop.Name}");
                parameters.Add(prop.Name, value);
                first = false;
            }

            return first ? ("", parameters) : (sb.ToString(), parameters);
        }

        private (string, string, string) GetInsertData(EntityMetadata metadata)
        {
            return (
                string.Join(", ", metadata.InsertProperties.Select(p => p.ColumnName)),
                string.Join(", ", metadata.InsertProperties.Select(p => $"@{p.Name}")),
                string.Join(", ", metadata.InsertProperties.Select(p => $"{p.ColumnName} = EXCLUDED.{p.ColumnName}"))
            );
        }

        private DynamicParameters CreateParameters(TEntity entity, EntityMetadata metadata)
        {
            var parameters = new DynamicParameters();
            foreach (var prop in metadata.Properties)
            {
                var value = prop.PropertyInfo.GetValue(entity);
                parameters.Add(prop.Name, ConvertValue(value));
            }
            return parameters;
        }

        private EntityMetadata GetMetadata()
        {
            return _metadataCache.GetOrAdd(typeof(TEntity), t =>
            {
                var props = t.GetProperties()
                    .Select(p => new EntityProperty(
                        p.Name,
                        p.GetCustomAttribute<ColumnAttribute>()?.ColumnName ?? p.Name.ToSnakeCase(),
                        p.PropertyType,
                        p.GetCustomAttributes(typeof(KeyAttribute), false).Any(),
                        p
                    )).ToList();

                var insertProperties = props.Where(p => !p.IsKey).ToList();

                return new EntityMetadata(
                    TableName: EntityMapper.GetTableName(new TEntity()),
                    AllColumns: string.Join(", ", props.Select(p => p.ColumnName)),
                    CopyCommand: $"COPY {EntityMapper.GetTableName(new TEntity())} ({string.Join(", ", insertProperties.Select(p => p.ColumnName))}) FROM STDIN (FORMAT BINARY)",
                    Properties: props,
                    InsertProperties: insertProperties
                );
            });
        }

        private void WriteValue(NpgsqlBinaryImporter writer, object value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNull();
                    break;
                case string str:
                    writer.Write(str);
                    break;
                case int i:
                    writer.Write(i);
                    break;
                case DateTime dt:
                    writer.Write(dt);
                    break;
                case bool b:
                    writer.Write(b);
                    break;
                case Enum e:
                    writer.Write((int)(object)e);
                    break;
                case IEnumerable<object> collection:
                    writer.Write(JsonConvert.SerializeObject(collection), NpgsqlDbType.Jsonb);
                    break;
                default:
                    writer.Write(value?.ToString());
                    break;
            }
        }

        private bool IsDefaultValue(object value)
        {
            if (value == null) return true;
            var type = value.GetType();
            return type.IsValueType &&
                   value.Equals(Activator.CreateInstance(type));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ConvertValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            if (type.IsEnum)
                return (int)value;

            if (value is System.Collections.IEnumerable enumerable && type != typeof(string))
            {
                return JsonConvert.SerializeObject(enumerable);
            }

            if (type.Namespace?.StartsWith("Helios.Database.Tables") == true)
            {
                return JsonConvert.SerializeObject(value);
            }

            return value;
        }
    }
}