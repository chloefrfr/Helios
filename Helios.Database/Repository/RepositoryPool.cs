using System;
using System.Collections.Concurrent;
using Helios.Database.Repository.Cache;
using Helios.Database.Tables;

namespace Helios.Database.Repository
{
    public class RepositoryPool
    {
        private readonly string _connectionUrl;

        public RepositoryPool(string connectionUrl)
        {
            _connectionUrl = connectionUrl;
        }

        public Repository<T> For<T>(bool cachingEnabled = true) where T : BaseTable, new()
        {
            return RepositoryCache<T>.Instance ??= new Repository<T>(_connectionUrl, cachingEnabled, TimeSpan.FromHours(8));
        }

        public Func<Repository<T>> Repo<T>(bool cachingEnabled = true) where T : BaseTable, new()
        {
            return RepositoryCache<T>.CachedFactory ??= () => For<T>(cachingEnabled);
        }
    }
}