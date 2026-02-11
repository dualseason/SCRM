using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;

        public SystemConfigService(HttpClient httpClient, ILocalStorageService localStorage)
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

        public async Task<IEnumerable<SystemConfig>> GetConfigsAsync()
        {
            try
            {
                await AddAuthorizationHeader();
                var configs = await _httpClient.GetFromJsonAsync<IEnumerable<SystemConfig>>("api/SystemConfig");
                return configs ?? Enumerable.Empty<SystemConfig>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching configs: {ex.Message}");
                return Enumerable.Empty<SystemConfig>();
            }
        }

        public async Task<SystemConfig?> GetConfigByKeyAsync(string key)
        {
            try
            {
                await AddAuthorizationHeader();
                return await _httpClient.GetFromJsonAsync<SystemConfig>($"api/SystemConfig/key/{key}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching config {key}: {ex.Message}");
                return null;
            }
        }

        public async Task<SystemConfig?> UpdateConfigAsync(SystemConfig config)
        {
            try
            {
                await AddAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync("api/SystemConfig", config);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SystemConfig>();
                }
                else
                {
                     Console.WriteLine($"Error updating config: {response.ReasonPhrase}");
                     return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating config: {ex.Message}");
                return null;
            }
        }
        public async Task<SCRM.SHARED.Models.SystemConfigModel> GetConfigModelAsync()
        {
            try
            {
                await AddAuthorizationHeader();
                return await _httpClient.GetFromJsonAsync<SCRM.SHARED.Models.SystemConfigModel>("api/SystemConfig/model") 
                       ?? new SCRM.SHARED.Models.SystemConfigModel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching config model: {ex.Message}");
                return new SCRM.SHARED.Models.SystemConfigModel();
            }
        }

        public async Task UpdateModelAsync(SCRM.SHARED.Models.SystemConfigModel model)
        {
            try
            {
                await AddAuthorizationHeader();
                await _httpClient.PostAsJsonAsync("api/SystemConfig/model", model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating config model: {ex.Message}");
            }
        }
    }
}
