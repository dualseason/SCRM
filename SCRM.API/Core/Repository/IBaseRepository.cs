using System.Linq.Expressions;

namespace SCRM.Core.Repository
{
    /// <summary>
    /// 通用仓储接口，提供基础CRUD操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    public interface IBaseRepository<TEntity, TKey> where TEntity : class
    {
        // Create
        Task<TEntity> AddAsync(TEntity entity);
        Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities);

        // Read
        Task<TEntity?> GetByIdAsync(TKey id);
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);
        Task<List<TEntity>> GetAllAsync();
        Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
        Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);

        // Update
        Task<TEntity> UpdateAsync(TEntity entity);
        Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities);

        // Delete
        Task<bool> DeleteAsync(TKey id);
        Task<bool> DeleteAsync(TEntity entity);
        Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate);

        // Pagination
        Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool ascending = true);

        // Soft Delete Support
        Task<bool> SoftDeleteAsync(TKey id);
        Task<int> SoftDeleteRangeAsync(Expression<Func<TEntity, bool>> predicate);
    }
}
