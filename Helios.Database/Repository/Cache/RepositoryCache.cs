using Helios.Database.Tables;

namespace Helios.Database.Repository.Cache;

public static class RepositoryCache<T> where T : BaseTable, new()
{
    public static Repository<T>? Instance;
    public static Func<Repository<T>>? CachedFactory;
}