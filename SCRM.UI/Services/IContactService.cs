using SCRM.API.Models.Entities;

namespace SCRM.UI.Services
{
    public interface IContactService
    {
        Task<PagedResult<Contact>> GetContactsAsync(int page, int pageSize, string? search = null);
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
    }
}
