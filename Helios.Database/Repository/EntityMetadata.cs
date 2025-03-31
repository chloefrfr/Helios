using System.Reflection;

namespace Helios.Database.Repository;

public record EntityProperty(
    string Name,
    string ColumnName,
    Type Type,
    bool IsKey,
    PropertyInfo PropertyInfo
);


public class EntityMetadata
{
    public string TableName { get; }
    public string AllColumns { get; }
    public string CopyCommand { get; }
    public List<EntityProperty> Properties { get; }
    public List<EntityProperty> InsertProperties { get; }
    public string InsertColumns { get; }
    public string InsertValues { get; }
    public string UpdateClause { get; }
    public EntityProperty[] FilterableProperties { get; }

    public EntityMetadata(
        string TableName, 
        string AllColumns, 
        string CopyCommand, 
        List<EntityProperty> Properties, 
        List<EntityProperty> InsertProperties,
        string InsertColumns = null,
        string InsertValues = null,
        string UpdateClause = null,
        EntityProperty[] FilterableProperties = null)
    {
        this.TableName = TableName;
        this.AllColumns = AllColumns;
        this.CopyCommand = CopyCommand;
        this.Properties = Properties;
        this.InsertProperties = InsertProperties;
        this.InsertColumns = InsertColumns;
        this.InsertValues = InsertValues;
        this.UpdateClause = UpdateClause;
        this.FilterableProperties = FilterableProperties;
    }
}