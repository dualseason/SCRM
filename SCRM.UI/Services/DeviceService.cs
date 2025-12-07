using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;

        public DeviceService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        private async Task AddAuthorizationHeader()
        {
            var token = await _localStorage.GetItemAsStringAsync("authToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim('"'));
            }
        }

        public async Task<IEnumerable<SrClient>> GetDevicesAsync()
        {
            try
            {
                await AddAuthorizationHeader();
                var clients = await _httpClient.GetFromJsonAsync<IEnumerable<SrClient>>("api/device");
                return clients ?? Enumerable.Empty<SrClient>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching devices: {ex.Message}");
                return Enumerable.Empty<SrClient>();
            }
        }

        public async Task<SrClient?> GetDeviceAsync(string id)
        {
            try
            {
                await AddAuthorizationHeader();
                return await _httpClient.GetFromJsonAsync<SrClient>($"api/device/{id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching device {id}: {ex.Message}");
                return null;
            }
        }
    }
}
