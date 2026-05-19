namespace SCRM.SHARED.Models.Dtos
{
    /// <summary>
    /// Android 客户端设备级配置下发模型。
    /// <para>
    /// 该模型直接对应 SmRun/62203 的 SetConfigTask(1382) 三类配置：
    /// 布尔配置、整数配置、字符串配置。这里只承载“本次需要下发的键”，
    /// 不做数据库持久化，避免把某台设备的临时设置误当成账号设置。
    /// </para>
    /// </summary>
    public class DeviceConfigDto
    {
        /// <summary>
        /// 布尔配置项，键名必须与 Android 端 SharedPreferences 配置键一致。
        /// </summary>
        public Dictionary<string, bool> BoolConfs { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 整数配置项，例如 keepWake、port。
        /// </summary>
        public Dictionary<string, int> IntConfs { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 字符串配置项，例如 host、fileUpUrl。
        /// </summary>
        public Dictionary<string, string> StrConfs { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 是否没有任何待下发配置。
        /// </summary>
        public bool IsEmpty =>
            BoolConfs.Count == 0 &&
            IntConfs.Count == 0 &&
            StrConfs.Count == 0;
    }
}
