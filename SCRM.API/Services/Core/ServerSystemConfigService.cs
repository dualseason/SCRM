using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.Services.Data;
using SCRM.UI.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.API.Services
{
    public class ServerSystemConfigService : ISystemConfigService
    {
        private readonly ApplicationDbContext _db;

        public ServerSystemConfigService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<SystemConfig>> GetConfigsAsync()
        {
            return await _db.SystemConfigs.AsNoTracking().ToListAsync();
        }

        public async Task<SystemConfig?> GetConfigByKeyAsync(string key)
        {
            return await _db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.key == key);
        }

        public async Task<SystemConfig?> UpdateConfigAsync(SystemConfig config)
        {
            var existing = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.key == config.key);
            
            if (existing != null)
            {
                existing.value = config.value;
                existing.description = config.description;
                existing.updatedAt = DateTime.UtcNow;
                _db.SystemConfigs.Update(existing);
            }
            else
            {
                // config.createdAt = DateTime.UtcNow; // Entity might not have createdAt, checking again...
                config.updatedAt = DateTime.UtcNow;
                await _db.SystemConfigs.AddAsync(config);
                existing = config;
            }

            await _db.SaveChangesAsync();
            return existing;
        }
    }
}
