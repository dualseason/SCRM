using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace SCRM.Core.Repository
{
    /// <summary>
    /// 通用仓储基类，实现基础CRUD操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    public class BaseRepository<TEntity, TKey> : IBaseRepository<TEntity, TKey> where TEntity : class
    {
        protected readonly DbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public BaseRepository(DbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        public virtual async Task<TEntity> AddAsync(TEntity entity)
        {
            var result = await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return result.Entity;
        }

        public virtual async Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities)
        {
            await _dbSet.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
            return entities;
        }

        public virtual async Task<TEntity?> GetByIdAsync(TKey id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<List<TEntity>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null)
        {
            return predicate == null
                ? await _dbSet.CountAsync()
                : await _dbSet.CountAsync(predicate);
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity)
        {
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities)
        {
            _dbSet.UpdateRange(entities);
            await _context.SaveChangesAsync();
            return entities;
        }

        public virtual async Task<bool> DeleteAsync(TKey id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
                return false;

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public virtual async Task<bool> DeleteAsync(TEntity entity)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public virtual async Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var entities = await FindAsync(predicate);
            _dbSet.RemoveRange(entities);
            return await _context.SaveChangesAsync();
        }

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool ascending = true)
        {
            var query = _dbSet.AsQueryable();

            // Apply filter
            if (predicate != null)
                query = query.Where(predicate);

            // Get total count
            int total = await query.CountAsync();

            // Apply ordering
            if (orderBy != null)
                query = ascending
                    ? query.OrderBy(orderBy)
                    : query.OrderByDescending(orderBy);

            // Apply pagination
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public virtual async Task<bool> SoftDeleteAsync(TKey id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
                return false;

            // Try to set is_deleted and deleted_at properties if they exist
            var isDeletedProperty = typeof(TEntity).GetProperty("IsDeleted");
            var deletedAtProperty = typeof(TEntity).GetProperty("DeletedAt");

            if (isDeletedProperty != null)
                isDeletedProperty.SetValue(entity, true);

            if (deletedAtProperty != null)
                deletedAtProperty.SetValue(entity, DateTime.UtcNow);

            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public virtual async Task<int> SoftDeleteRangeAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var entities = await FindAsync(predicate);

            foreach (var entity in entities)
            {
                var isDeletedProperty = typeof(TEntity).GetProperty("IsDeleted");
                var deletedAtProperty = typeof(TEntity).GetProperty("DeletedAt");

                if (isDeletedProperty != null)
                    isDeletedProperty.SetValue(entity, true);

                if (deletedAtProperty != null)
                    deletedAtProperty.SetValue(entity, DateTime.UtcNow);
            }

            _dbSet.UpdateRange(entities);
            return await _context.SaveChangesAsync();
        }
    }
}
