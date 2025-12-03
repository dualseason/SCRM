using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public interface IDeviceService
    {
        Task<IEnumerable<Device>> GetDevicesAsync();
        Task<Device?> GetDeviceAsync(int id);
    }
}
