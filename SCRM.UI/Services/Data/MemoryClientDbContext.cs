using Microsoft.Extensions.Caching.Memory;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;
using System.Collections.Concurrent;

namespace SCRM.UI.Services.Data;

public class MemoryClientDbContext : IClientDbContext
{
    public MemoryClientDbContext()
    {
        Contacts = new MemoryDataSet<Contact>();
        Messages = new MemoryDataSet<Message>();
    }

    public IClientDataSet<Contact> Contacts { get; }
    public IClientDataSet<Message> Messages { get; }
}

public class MemoryDataSet<T> : IClientDataSet<T> where T : class
{
    // Simple in-memory store
    private readonly ConcurrentDictionary<object, T> _store = new();

    public Task SaveAsync(T entity, object key)
    {
        _store[key] = entity;
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync(object key)
    {
        if (_store.TryGetValue(key, out var val))
        {
            return Task.FromResult<T?>(val);
        }
        return Task.FromResult<T?>(null);
    }

    public Task<List<T>> GetAllAsync()
    {
        return Task.FromResult(_store.Values.ToList());
    }

    public Task<List<T>> GetByIndexAsync<TKey>(string indexName, TKey value)
    {
        var all = _store.Values.ToList();
         var prop = typeof(T).GetProperties().FirstOrDefault(p => p.Name.Equals(indexName, StringComparison.OrdinalIgnoreCase));
         if (prop != null)
         {
             var result = all.Where(item => 
             {
                 var val = prop.GetValue(item);
                 if (val == null) return value == null;
                 return val.Equals(value); 
             }).ToList();
             return Task.FromResult(result);
         }
         return Task.FromResult(new List<T>());
    }

    public Task DeleteAsync(object key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
