using Microsoft.EntityFrameworkCore;
using SCRM.API.Models;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Data;
using SCRM.Services.Data;
using SCRM.SHARED.Models;

namespace SCRM.API.Services.Data
{
    /// <summary>
    /// 缓存预热服务 - KISS原则
    /// 服务器启动时主动将数据库中的关键数据同步到内存缓存中，
    /// 确保 DbHelper.GetAllSrClients() 等同步方法能立即返回数据。
    /// </summary>
    public class CacheWarmupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheWarmupService> _logger;

        public CacheWarmupService(IServiceProvider serviceProvider, ILogger<CacheWarmupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在进行缓存预热...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. 预热 SrClient
                List<SrClient> clients = await db.SrClients
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                foreach (var client in clients)
                {
                    GlobalCache.srClients.TryAdd(client.uuid, client);
                }

                _logger.LogInformation($"已预热 {clients.Count} 个设备到 GlobalCache。");

                // 2. 预热 WechatAccount，并把非持久化 Wx 展示结构回填到对应设备。
                //    这样服务端重启后，IM 中心/设备列表不会因为 SrClient.wx 未初始化而显示“微信未登录”。
                List<WechatAccount> accounts = await db.WechatAccounts
                    .AsNoTracking()
                    .Where(a => !a.isDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var account in accounts)
                {
                    if (string.IsNullOrWhiteSpace(account.wxid))
                    {
                        continue;
                    }

                    GlobalCache.wechatAccounts.AddOrUpdate(account.wxid, account, (_, old) => old.CopyFrom(account));

                    if (!string.IsNullOrWhiteSpace(account.clientUuid)
                        && GlobalCache.srClients.TryGetValue(account.clientUuid, out var client)
                        && client.wx?.wechatAccount == null)
                    {
                        client.wx = new Wx
                        {
                            srClient = client,
                            wechatAccount = account
                        };
                    }
                }

                _logger.LogInformation($"已预热 {accounts.Count} 个微信账号到 GlobalCache。");


            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
