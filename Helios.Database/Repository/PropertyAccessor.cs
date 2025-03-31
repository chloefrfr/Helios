using Helios.Database.Tables;

namespace Helios.Database.Repository;

internal class PropertyAccessor<TEntity> where TEntity : BaseTable, new()
{
    public EntityProperty Property { get; }
    public Func<TEntity, object> Getter { get; }
    public Action<TEntity, object> Setter { get; }
        
    public PropertyAccessor(EntityProperty property, Func<TEntity, object> getter, Action<TEntity, object> setter)
    {
        Property = property;
        Getter = getter;
        Setter = setter;
    }
}