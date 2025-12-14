namespace SCRM.SHARED.Models
{
    /// <summary>
    /// 支持缓存同步的实体接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface ICacheable<T>
    {
        /// <summary>
        /// 获取主键ID (用于锁 Key 和 缓存 Key)
        /// </summary>
        string GetId();

        /// <summary>
        /// 从另一个实例复制数据 (用于缓存热更新)
        /// </summary>
        T CopyFrom(T other);
    }
}
