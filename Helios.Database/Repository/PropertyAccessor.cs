using Helios.Database.Tables;

namespace Helios.Database.Repository;

internal class PropertyAccessor<T> where T : class
{
    public EntityProperty Property { get; }
    public Func<T, object> Getter { get; }
    public Action<T, object> Setter { get; }

    public PropertyAccessor(EntityProperty property, Func<T, object> getter, Action<T, object> setter)
    {
        Property = property;
        Getter = getter;
        Setter = setter;
    }
}