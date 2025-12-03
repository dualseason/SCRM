using System.Net.Http.Json;
using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly HttpClient _httpClient;

        public DeviceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<Device>> GetDevicesAsync()
        {
            try
            {
                var devices = await _httpClient.GetFromJsonAsync<IEnumerable<Device>>("api/device");
                return devices ?? Enumerable.Empty<Device>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching devices: {ex.Message}");
                return Enumerable.Empty<Device>();
            }
        }

        public async Task<Device?> GetDeviceAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Device>($"api/device/{id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching device {id}: {ex.Message}");
                return null;
            }
        }
    }
}
