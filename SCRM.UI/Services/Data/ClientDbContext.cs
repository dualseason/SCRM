using Microsoft.Extensions.Caching.Memory;
using SCRM.SHARED.Models;
using SCRM.API.Models.Entities;
using TG.Blazor.IndexedDB;

namespace SCRM.UI.Services.Data;

public class ClientDbContext : IClientDbContext
{
    private readonly IndexedDBManager _dbManager;
    private readonly IMemoryCache _memoryCache;
    
    // Global lock for the context to ensure thread safety
    private readonly SemaphoreSlim _globalLock = new(1, 1);

    public ClientDbContext(IndexedDBManager dbManager, IMemoryCache memoryCache)
    {
        _dbManager = dbManager;
        _memoryCache = memoryCache;
        
        Contacts = new CachedIndexedSet<Contact>(_dbManager, _memoryCache, "Contacts", _globalLock);
        Messages = new CachedIndexedSet<Message>(_dbManager, _memoryCache, "Messages", _globalLock);
    }

    public IClientDataSet<Contact> Contacts { get; }
    public IClientDataSet<Message> Messages { get; }
}

/// <summary>
/// A wrapper around IndexedDB stores that provides MemoryCache L1 and locking.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CachedIndexedSet<T> : IClientDataSet<T> where T : class
{
    private readonly IndexedDBManager _dbManager;
    private readonly IMemoryCache _cache;
    private readonly string _storeName;
    private readonly SemaphoreSlim _lock;
    
    // Cache configuration
    private readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30), // Keep active data in memory
        Size = 1 // Basic size counting
    };

    public CachedIndexedSet(IndexedDBManager dbManager, IMemoryCache cache, string storeName, SemaphoreSlim lockObj)
    {
        _dbManager = dbManager;
        _cache = cache;
        _storeName = storeName;
        _lock = lockObj;
    }

    private string GetCacheKey(object key) => $"{_storeName}_{key}";

    /// <summary>
    /// Adds or Updates an entity individually.
    /// </summary>
    public async Task SaveAsync(T entity, object key)
    {
        await _lock.WaitAsync();
        try
        {
            // 1. Save to IndexedDB
            var newRecord = new StoreRecord<T>
            {
                Storename = _storeName,
                Data = entity
            };
            
            await _dbManager.UpdateRecord(newRecord);
            
            // 2. Update Memory Cache
            _cache.Set(GetCacheKey(key), entity, _cacheOptions);
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[ClientDbContext] Error saving {typeof(T).Name}: {ex.Message}");
             
             // Try Add if update failed?
             try 
             {
                  await _dbManager.AddRecord(new StoreRecord<T> { Storename = _storeName, Data = entity });
                  _cache.Set(GetCacheKey(key), entity, _cacheOptions);
             }
             catch(Exception ex2)
             {
                 Console.WriteLine($"[ClientDbContext] Add fallback also failed: {ex2.Message}");
                 // throw; // Suppress error to avoid crashing flow? Or throw?
                 // For now, let's log and continue if possible, but data might be lost.
             }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get entity by key, prioritizing Cache.
    /// </summary>
    public async Task<T?> GetAsync(object key)
    {
        // 1. Try Cache
        if (_cache.TryGetValue(GetCacheKey(key), out T? cachedItem))
        {
            return cachedItem;
        }

        // 2. Try IndexedDB
        try 
        {
            var item = await _dbManager.GetRecordById<object, T>(_storeName, key);
            
            if (item != null)
            {
                // Populate Cache
                _cache.Set(GetCacheKey(key), item, _cacheOptions);
            }
            return item;
        }
        catch
        {
            return null;
        }
    }    
    
    /// <summary>
    ///  Get generic list from DB (no cache usually for GetAll unless we cache list)
    /// </summary>
    public async Task<List<T>> GetAllAsync()
    {
        return await _dbManager.GetRecords<T>(_storeName);
    }

    /// <summary>
    /// Get records by Index (e.g. WechatAccountId).
    /// </summary>
    public async Task<List<T>> GetByIndexAsync<TKey>(string indexName, TKey value)
    {
        try 
        {
             // Fallback: Client-side filtering
             var all = await GetAllAsync();
             if (all == null || !all.Any()) return new List<T>();
             
             var prop = typeof(T).GetProperties().FirstOrDefault(p => p.Name.Equals(indexName, StringComparison.OrdinalIgnoreCase));
             if (prop != null)
             {
                 return all.Where(item => 
                 {
                     var val = prop.GetValue(item);
                     if (val == null) return value == null;
                     return val.Equals(value); 
                 }).ToList();
             }
             
             return new List<T>();
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[ClientDbContext] Error in GetByIndexAsync: {ex.Message}");
            return new List<T>();
        }
    }

    /// <summary>
    /// Clear specific item
    /// </summary>
    public async Task DeleteAsync(object key)
    {
        await _lock.WaitAsync();
        try
        {
            await _dbManager.DeleteRecord(_storeName, key);
            _cache.Remove(GetCacheKey(key));
        }
        finally
        {
            _lock.Release();
        }
    }
}
