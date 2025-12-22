using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.UI.Services.Data
{
    public class EfClientDataSet<T> : IClientDataSet<T> where T : class
    {
        private readonly DbContext _context;
        private readonly DbSet<T> _dbSet;

        public EfClientDataSet(DbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task SaveAsync(T entity, object key)
        {
            // Upsert logic
            // Check if exists
            var existing = await _dbSet.FindAsync(key);
            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(entity);
            }
            else
            {
                await _dbSet.AddAsync(entity);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<T?> GetAsync(object key)
        {
            return await _dbSet.FindAsync(key);
        }

        public async Task<List<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<List<T>> GetByIndexAsync<TKey>(string indexName, TKey value)
        {
            // Dynamic query using EF.Property
            // Note: EF.Property requires known type for value usually, but generic should work if matches definition.
            // Using logic: Where(e => EF.Property<TKey>(e, indexName).Equals(value))
            
            try 
            {
                return await _dbSet.Where(e => EF.Property<TKey>(e, indexName).Equals(value)).ToListAsync();
            }
            catch
            {
                // Fallback or error if property doesn't exist/type mismatch
                return new List<T>();
            }
        }

        public async Task DeleteAsync(object key)
        {
            var entity = await _dbSet.FindAsync(key);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
