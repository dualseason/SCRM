using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.UI.Services.Data
{
    public interface IClientDataSet<T>
    {
        Task SaveAsync(T entity, object key);
        Task<T?> GetAsync(object key);
        Task<List<T>> GetAllAsync();
        Task<List<T>> GetByIndexAsync<TKey>(string indexName, TKey value);
        Task DeleteAsync(object key);
    }
}
