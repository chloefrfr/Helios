using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
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
        
        private readonly EntityMetadata _metadata;
        private readonly string _insertSql;
        private readonly string _insertReturnIdSql;
        private readonly string _deleteBaseSql;
        private readonly string _updateBaseSql;
        private readonly string _findBaseSql;
        private readonly string _findAllBaseSql;
        
        private readonly Dictionary<string, Func<TEntity, object>> _propertyGetters;
        private readonly Dictionary<string, Action<TEntity, object>> _propertySetters;
        private readonly List<PropertyAccessor<TEntity>> _fastPropertyAccessors;
        
        private readonly NpgsqlConnection[] _connectionPool;
        private readonly int _poolSize = 5; 
        private int _nextConnectionIndex = 0;

        public Repository(string connectionString)
        {
            _connectionString = connectionString;
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            if (!SqlMapper.HasTypeHandler(typeof(List<string>)))
            {
                SqlMapper.AddTypeHandler(new JsonListManager());
            }
            
            if (!SqlMapper.HasTypeHandler(typeof(TEntity)))
            {
                SqlMapper.SetTypeMap(typeof(TEntity), new CustomPropertyTypeMap(typeof(TEntity),
                    (type, columnName) => type.GetProperties().FirstOrDefault(prop =>
                        prop.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))));
            }
            
            _metadata = GetMetadata();
            
            _propertyGetters = new Dictionary<string, Func<TEntity, object>>(_metadata.Properties.Count);
            _propertySetters = new Dictionary<string, Action<TEntity, object>>(_metadata.Properties.Count);
            _fastPropertyAccessors = new List<PropertyAccessor<TEntity>>(_metadata.Properties.Count);
            
            foreach (var prop in _metadata.Properties)
            {
                _propertyGetters[prop.Name] = CompileGetter(prop.PropertyInfo);
                _propertySetters[prop.Name] = CompileSetter(prop.PropertyInfo);
                _fastPropertyAccessors.Add(new PropertyAccessor<TEntity>(
                    prop,
                    _propertyGetters[prop.Name],
                    _propertySetters[prop.Name]
                ));
            }
            
            var columns = string.Join(", ", _metadata.InsertProperties.Select(p => p.ColumnName));
            var values = string.Join(", ", _metadata.InsertProperties.Select(p => $"@{p.Name}"));
            var updates = string.Join(", ", _metadata.InsertProperties.Select(p => $"{p.ColumnName} = EXCLUDED.{p.ColumnName}"));
            
            _insertSql = $"INSERT INTO {_metadata.TableName} ({columns}) VALUES ({values}) ON CONFLICT (id) DO UPDATE SET {updates}";
            _insertReturnIdSql = $"{_insertSql} RETURNING id";
            _deleteBaseSql = $"DELETE FROM {_metadata.TableName} WHERE ";
            _updateBaseSql = $"UPDATE {_metadata.TableName} SET ";
            _findBaseSql = $"SELECT {_metadata.AllColumns} FROM {_metadata.TableName} WHERE ";
            _findAllBaseSql = $"SELECT {_metadata.AllColumns} FROM {_metadata.TableName} WHERE ";
            
            _connectionPool = new NpgsqlConnection[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                _connectionPool[i] = new NpgsqlConnection(_connectionString);
                _connectionPool[i].Open();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NpgsqlConnection GetConnection()
        {
            var index = Interlocked.Increment(ref _nextConnectionIndex) % _poolSize;
            var conn = _connectionPool[index];
            
            if (conn.State != ConnectionState.Open)
            {
                lock (conn)
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Close();
                        conn.Open();
                    }
                }
            }
            
            return conn;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<TEntity> FindAsync(TEntity template, int timeout = 500)
        {
            var (whereClause, parameters) = BuildFastWhereClause(template);
            if (string.IsNullOrEmpty(whereClause)) 
                return default;
            
            var sql = _findBaseSql + whereClause + " LIMIT 1";
            var conn = GetConnection();
            
            try
            {
                return await conn.QueryFirstOrDefaultAsync<TEntity>(
                    sql, parameters, commandTimeout: timeout).ConfigureAwait(false);
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                return await newConn.QueryFirstOrDefaultAsync<TEntity>(
                    sql, parameters, commandTimeout: timeout).ConfigureAwait(false);
            }
        }

        public async Task<int> UpdateAsync(TEntity entity)
        {
            var keyProp = _metadata.Properties.FirstOrDefault(p => p.IsKey);
            if (keyProp == null)
                throw new InvalidOperationException("Entity must have a primary key defined for updates.");

            var nonKeyProps = _metadata.Properties.Where(p => !p.IsKey).ToArray();
            var keyValue = _propertyGetters[keyProp.Name](entity);
            
            if (keyValue == null)
                throw new InvalidOperationException("Primary key value cannot be null for updates.");
            
            var sb = StringBuilderCache.Acquire(256);
            sb.Append(_updateBaseSql);
            
            var parameters = new Dictionary<string, object>(nonKeyProps.Length + 1);
            parameters[keyProp.Name] = ConvertValue(keyValue);
            
            for (int i = 0; i < nonKeyProps.Length; i++)
            {
                var prop = nonKeyProps[i];
                var value = _propertyGetters[prop.Name](entity);
                parameters[prop.Name] = ConvertValue(value);
                
                sb.Append(prop.ColumnName);
                sb.Append(" = @");
                sb.Append(prop.Name);
                
                if (i < nonKeyProps.Length - 1)
                    sb.Append(", ");
            }
            
            sb.Append(" WHERE ");
            sb.Append(keyProp.ColumnName);
            sb.Append(" = @");
            sb.Append(keyProp.Name);
            
            var sql = StringBuilderCache.GetStringAndRelease(sb);
            var conn = GetConnection();
            
            try
            {
                return await conn.ExecuteAsync(sql, parameters).ConfigureAwait(false);
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                return await newConn.ExecuteAsync(sql, parameters).ConfigureAwait(false);
            }
        }

        public async Task<int> DeleteAsync(TEntity template)
        {
            var (whereClause, parameters) = BuildFastWhereClause(template);
            
            if (string.IsNullOrEmpty(whereClause))
                throw new InvalidOperationException("No conditions provided for deletion. To delete all records, use DeleteAllAsync.");
            
            var sql = _deleteBaseSql + whereClause;
            var conn = GetConnection();
            
            try
            {
                return await conn.ExecuteAsync(sql, parameters).ConfigureAwait(false);
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                return await newConn.ExecuteAsync(sql, parameters).ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<TEntity>> FindAllAsync(TEntity template, int limit = 5000)
        {
            var (whereClause, parameters) = BuildFastWhereClause(template);
            
            var sql = string.IsNullOrEmpty(whereClause) 
                ? $"SELECT {_metadata.AllColumns} FROM {_metadata.TableName} LIMIT {limit}"
                : _findAllBaseSql + whereClause + $" LIMIT {limit}";
            
            var conn = GetConnection();
            
            try
            {
                return await conn.QueryAsync<TEntity>(sql, parameters).ConfigureAwait(false);
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                return await newConn.QueryAsync<TEntity>(sql, parameters).ConfigureAwait(false);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<List<TEntity>> FindAllByTableAsync(int limit = 5000)
        {
            var sql = $"SELECT {_metadata.AllColumns} FROM {_metadata.TableName} LIMIT {limit}";
            var conn = GetConnection();
    
            try
            {
                var result = await conn.QueryAsync<TEntity>(
                        sql, 
                        commandTimeout: 30) 
                    .ConfigureAwait(false);
                return result.AsList();
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                var result = await newConn.QueryAsync<TEntity>(
                        sql,
                        commandTimeout: 30)
                    .ConfigureAwait(false);
                return result.AsList();
            }
        }
        
        public async Task<List<TEntity>> FindManyAsync(TEntity template, int limit = 1000)
        {
            var (whereClause, parameters) = BuildFastWhereClause(template);

            var sql = string.IsNullOrEmpty(whereClause)
                ? $"SELECT {_metadata.AllColumns} FROM {_metadata.TableName} LIMIT {limit}"
                : $"{_findBaseSql}{whereClause} LIMIT {limit}";

            var conn = GetConnection();

            try
            {
                var result = await conn.QueryAsync<TEntity>(sql, parameters).ConfigureAwait(false);
                return result.AsList();
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                var result = await newConn.QueryAsync<TEntity>(sql, parameters).ConfigureAwait(false);
                return result.AsList();
            }
        }

        public async Task<int> SaveAsync(TEntity entity, bool returnId = true)
        {
            var parameters = new Dictionary<string, object>(_metadata.Properties.Count);
            foreach (var accessor in _fastPropertyAccessors)
            {
                var value = accessor.Getter(entity);
                parameters[accessor.Property.Name] = ConvertValue(value);
            }
            
            var conn = GetConnection();
            
            try
            {
                if (returnId)
                {
                    var newId = await conn.ExecuteScalarAsync<int>(_insertReturnIdSql, parameters).ConfigureAwait(false);
                    
                    var idProperty = _metadata.Properties.FirstOrDefault(p => p.IsKey);
                    if (idProperty != null)
                    {
                        _propertySetters[idProperty.Name](entity, newId);
                    }
                    
                    return newId;
                }
                
                return await conn.ExecuteAsync(_insertSql, parameters).ConfigureAwait(false);
            }
            catch
            {
                using var newConn = CreateConnection();
                await newConn.OpenAsync().ConfigureAwait(false);
                
                if (returnId)
                {
                    var newId = await newConn.ExecuteScalarAsync<int>(_insertReturnIdSql, parameters).ConfigureAwait(false);
                    
                    var idProperty = _metadata.Properties.FirstOrDefault(p => p.IsKey);
                    if (idProperty != null)
                    {
                        _propertySetters[idProperty.Name](entity, newId);
                    }
                    
                    return newId;
                }
                
                return await newConn.ExecuteAsync(_insertSql, parameters).ConfigureAwait(false);
            }
        }

        public async Task BulkInsertAsync(IEnumerable<TEntity> entities)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync().ConfigureAwait(false);
    
            var entitiesArray = entities as TEntity[] ?? entities.ToArray();
            if (entitiesArray.Length == 0) return;

            using var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);
    
            try
            {
                for (int i = 0; i < entitiesArray.Length; i += MaxBatchSize)
                {
                    var remainingCount = Math.Min(MaxBatchSize, entitiesArray.Length - i);
            
                    using var writer = conn.BeginBinaryImport(_metadata.CopyCommand);
            
                    for (int j = i; j < i + remainingCount; j++)
                    {
                        writer.StartRow();
                        foreach (var prop in _metadata.InsertProperties)
                        {
                            var value = _propertyGetters[prop.Name](entitiesArray[j]);
                            WriteValueFast(writer, value, prop.Type);
                        }
                    }
            
                    await writer.CompleteAsync().ConfigureAwait(false);
                }
        
                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Bulk insert failed. See inner exception.", ex);
            }
        }
        
        public async Task BulkUpdateAsync(IEnumerable<TEntity> entities)
        {
            var keyProp = _metadata.Properties.FirstOrDefault(p => p.IsKey);
            if (keyProp == null)
                throw new InvalidOperationException("Entity must have a primary key for bulk updates.");

            var entitiesList = entities.ToList();
            if (entitiesList.Count == 0)
                return;

            var nonKeyProps = _metadata.Properties.Where(p => !p.IsKey).ToList();
            if (nonKeyProps.Count == 0)
                return;

            using var conn = CreateConnection();
            await conn.OpenAsync().ConfigureAwait(false);
            
            using var transaction = await conn.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                for (int batchOffset = 0; batchOffset < entitiesList.Count; batchOffset += MaxBatchSize)
                {
                    var batchEntities = entitiesList
                        .Skip(batchOffset)
                        .Take(MaxBatchSize)
                        .ToList();

                    var parameters = new DynamicParameters();
                    var valueStrings = new List<string>();
                    
                    for (int i = 0; i < batchEntities.Count; i++)
                    {
                        var entity = batchEntities[i];
                        var keyValue = _propertyGetters[keyProp.Name](entity);
                        if (keyValue == null)
                            throw new InvalidOperationException("Primary key cannot be null for bulk updates.");
                        
                        parameters.Add($"id_{i}", ConvertValue(keyValue));
                        
                        foreach (var prop in nonKeyProps)
                        {
                            var propValue = ConvertValue(_propertyGetters[prop.Name](entity));
                            parameters.Add($"{prop.Name}_{i}", propValue);
                        }
                        
                        var valuePlaceholders = string.Join(", ", nonKeyProps.Select(p => $"@{p.Name}_{i}"));
                        valueStrings.Add($"(@id_{i}, {valuePlaceholders})");
                    }

                    var columns = string.Join(", ", nonKeyProps.Select(p => p.ColumnName));
                    var sql = $@"
                        UPDATE {_metadata.TableName} 
                        SET 
                            {string.Join(", ", nonKeyProps.Select(p => $"{p.ColumnName} = data.{p.ColumnName}"))}
                        FROM (VALUES 
                            {string.Join(", ", valueStrings)}
                        ) AS data ({keyProp.ColumnName}, {columns})
                        WHERE {_metadata.TableName}.{keyProp.ColumnName} = data.{keyProp.ColumnName}";

                    await conn.ExecuteAsync(sql, parameters, transaction).ConfigureAwait(false);
                }
                
                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Bulk update failed. See inner exception.", ex);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (string, Dictionary<string, object>) BuildFastWhereClause(TEntity template)
        {
            var parameters = new Dictionary<string, object>();
            var sb = StringBuilderCache.Acquire(128);
            var first = true;
            
            foreach (var accessor in _fastPropertyAccessors)
            {
                if (accessor.Property.IsKey) continue;
                
                var value = accessor.Getter(template);
                if (IsDefaultValue(value)) continue;
                
                if (first)
                    first = false;
                else
                    sb.Append(" AND ");
                
                sb.Append(accessor.Property.ColumnName);
                sb.Append(" = @");
                sb.Append(accessor.Property.Name);
                
                parameters[accessor.Property.Name] = ConvertValue(value);
            }
            
            return first ? ("", parameters) : (StringBuilderCache.GetStringAndRelease(sb), parameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteValueFast(NpgsqlBinaryImporter writer, object value, Type propertyType)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            
            if (propertyType == typeof(string))
            {
                writer.Write((string)value);
                return;
            }
            
            if (propertyType == typeof(int))
            {
                writer.Write((int)value);
                return;
            }
            
            if (propertyType == typeof(bool))
            {
                writer.Write((bool)value);
                return;
            }
            
            if (propertyType == typeof(DateTime))
            {
                writer.Write((DateTime)value);
                return;
            }
            
            if (propertyType.IsEnum)
            {
                writer.Write(Convert.ToInt32(value));
                return;
            }
            
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                writer.Write(JsonConvert.SerializeObject(value), NpgsqlDbType.Jsonb);
                return;
            }
            
            if (propertyType.Namespace?.StartsWith("Helios.Database.Tables") == true)
            {
                writer.Write(JsonConvert.SerializeObject(value), NpgsqlDbType.Jsonb);
                return;
            }
            
            if (value is System.Collections.IEnumerable && propertyType != typeof(string))
            {
                writer.Write(JsonConvert.SerializeObject(value), NpgsqlDbType.Jsonb);
                return;
            }
            
            writer.Write(value.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsDefaultValue(object value)
        {
            if (value == null) return true;
            
            var type = value.GetType();
            if (!type.IsValueType) return false;
            
            if (type == typeof(int)) return (int)value == 0;
            if (type == typeof(long)) return (long)value == 0;
            if (type == typeof(bool)) return (bool)value == false;
            if (type == typeof(DateTime)) return (DateTime)value == default;
            if (type == typeof(float)) return (float)value == 0;
            if (type == typeof(double)) return (double)value == 0;
            if (type == typeof(decimal)) return (decimal)value == 0;
            if (type == typeof(Guid)) return (Guid)value == Guid.Empty;
            
            return value.Equals(Activator.CreateInstance(type));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ConvertValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            if (type.IsEnum)
                return Convert.ToInt32(value);

            if (type.IsArray)
                return value;

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
        
        private Func<TEntity, object> CompileGetter(PropertyInfo propertyInfo)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "obj");
            var property = Expression.Property(Expression.Convert(parameter, propertyInfo.DeclaringType), propertyInfo);
            var convert = Expression.Convert(property, typeof(object));
            
            return Expression.Lambda<Func<TEntity, object>>(convert, parameter).Compile();
        }
        
        private Action<TEntity, object> CompileSetter(PropertyInfo propertyInfo)
        {
            var objParam = Expression.Parameter(typeof(TEntity), "obj");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var convertedValue = Expression.Convert(valueParam, propertyInfo.PropertyType);
            var propertyAccess = Expression.Property(objParam, propertyInfo);
            var assign = Expression.Assign(propertyAccess, convertedValue);
            return Expression.Lambda<Action<TEntity, object>>(assign, objParam, valueParam).Compile();
        }
    }
}