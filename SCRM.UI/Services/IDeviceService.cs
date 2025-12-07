using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public interface IDeviceService
    {
        Task<IEnumerable<SrClient>> GetDevicesAsync();
        Task<SrClient?> GetDeviceAsync(string id);
    }
}
