using System.Reflection;

namespace Helios.Database.Repository;

public record EntityMetadata(
    string TableName,
    string AllColumns,
    string CopyCommand,
    List<EntityProperty> Properties,
    List<EntityProperty> InsertProperties
);

public record EntityProperty(
    string Name,
    string ColumnName,
    Type Type,
    bool IsKey,
    PropertyInfo PropertyInfo
);