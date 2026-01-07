using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public interface ISystemConfigService
    {
        Task<IEnumerable<SystemConfig>> GetConfigsAsync();
        Task<SystemConfig?> GetConfigByKeyAsync(string key);
        Task<SystemConfig?> UpdateConfigAsync(SystemConfig config);
    }
}
