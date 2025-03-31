using Helios.Database.Tables;

namespace Helios.Database.Repository
{
    public class RepositoryPool
    {
        private readonly Dictionary<Type, object> _repositories = new();
        private readonly string _connectionUrl;

        public RepositoryPool(string connectionUrl)
        {
            _connectionUrl = connectionUrl;
        }

        public Repository<T> GetRepository<T>() where T : BaseTable, new()
        {
            if (!_repositories.ContainsKey(typeof(T)))
            {
                _repositories[typeof(T)] = new Repository<T>(_connectionUrl);
            }
            return (Repository<T>)_repositories[typeof(T)];
        }
    }
}