using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public class ContactService : IContactService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;

        public ContactService(HttpClient httpClient, ILocalStorageService localStorage)
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

        public async Task<PagedResult<Contact>> GetContactsAsync(int page, int pageSize, string? search = null)
        {
            try
            {
                await AddAuthorizationHeader();
                var url = $"api/Contacts?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                {
                    url += $"&search={Uri.EscapeDataString(search)}";
                }

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var items = await response.Content.ReadFromJsonAsync<IEnumerable<Contact>>();
                    var totalCountHeader = response.Headers.FirstOrDefault(h => h.Key == "X-Total-Count").Value?.FirstOrDefault();
                    int totalCount = 0;
                    if (totalCountHeader != null)
                    {
                        int.TryParse(totalCountHeader, out totalCount);
                    }

                    return new PagedResult<Contact>
                    {
                        Items = items ?? Enumerable.Empty<Contact>(),
                        TotalCount = totalCount
                    };
                }
                else
                {
                    Console.WriteLine($"Error fetching contacts: {response.ReasonPhrase}");
                    return new PagedResult<Contact> { Items = Enumerable.Empty<Contact>(), TotalCount = 0 };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching contacts: {ex.Message}");
                return new PagedResult<Contact> { Items = Enumerable.Empty<Contact>(), TotalCount = 0 };
            }
        }
    }
}
