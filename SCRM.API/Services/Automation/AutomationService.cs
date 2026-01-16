using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SCRM.Services.Events;
using SCRM.API.Services.Data;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using SCRM.API.Models.Events;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using SCRM.API.Services.Core;

namespace SCRM.Services.Automation
{
    /// <summary>
    /// 自动化后台服务 (C&C 逻辑)
    /// </summary>
    public class AutomationService : BackgroundService
    {
        private readonly IEventBus _eventBus;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ClientTaskService _clientTaskService;
        private readonly ILogger<AutomationService> _logger;
        
        // 订阅令牌
        private IDisposable? _messageSub;
        private IDisposable? _friendRequestSub;
        private IDisposable? _circleSub;

        public AutomationService(
            IEventBus eventBus,
            IServiceScopeFactory scopeFactory,
            ClientTaskService clientTaskService,
            ILogger<AutomationService> logger)
        {
            _eventBus = eventBus;
            _scopeFactory = scopeFactory;
            _clientTaskService = clientTaskService;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("自动化服务已启动 (C&C 逻辑).");

            // 1. 订阅聊天消息 (用于 UI 转发/基础日志)
            _messageSub = _eventBus.Subscribe<MessageReceivedEvent>(async (e) =>
            {
               try 
               {
                   // 可以在此添加简单的消息处理逻辑或统计
                   await Task.CompletedTask; 
               }
               catch(Exception ex) { _logger.LogError(ex, "自动化服务处理消息事件出错"); }
            });

            // 2. 订阅好友请求事件
            _friendRequestSub = _eventBus.Subscribe<FriendRequestEvent>(async (e) =>
            {
                await HandleFriendRequest(e);
            });

            // 3. 订阅朋友圈发布事件
            _circleSub = _eventBus.Subscribe<CircleNewPublishEvent>(async (e) =>
            {
                await HandleCircleNewPublish(e);
            });
            
            // 4. 订阅显式的自动化消息事件 (自动回复/红包)
            _eventBus.Subscribe<AutomationMessageEvent>(async (e) =>
            {
                await HandleMessageAutomation(e.accountId, e.connectionId, e.weChatId, e.friendId, e.contentXml, e.contentType);
            });
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// 处理好友请求自动化
        /// </summary>
        private async Task HandleFriendRequest(FriendRequestEvent e)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var account = await db.GetWechatAccount(e.accountId);
                    if (account != null && !string.IsNullOrEmpty(account.settings))
                    {
                        var settings = JsonSerializer.Deserialize<WechatAccountSettings>(account.settings);
                        if (settings != null && settings.AutoAcceptFriendRequest)
                        {
                            _logger.LogInformation("自动接受好友请求: 来自 {Friend}, 账号 {WeChat}", e.friendNick, e.weChatId);
                            long taskId = DateTime.UtcNow.Ticks;
                            await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(e.connectionId, e.friendId, e.friendNick, taskId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理好友请求自动化时出错");
            }
        }

        /// <summary>
        /// 处理朋友圈自动化 (如自动点赞)
        /// </summary>
        private async Task HandleCircleNewPublish(CircleNewPublishEvent e)
        {
             try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var account = await db.GetWechatAccount(e.accountId);
                    if (account != null && !string.IsNullOrEmpty(account.settings))
                    {
                        var settings = JsonSerializer.Deserialize<WechatAccountSettings>(account.settings);
                        if (settings != null && settings.AutoLikeMoments)
                        {
                            _logger.LogInformation("自动点赞朋友圈 {CircleId}, 账号 {WeChat}", e.circleId, e.weChatId);
                            long taskId = DateTime.UtcNow.Ticks;
                            await _clientTaskService.SendCircleLikeTaskAsync(e.connectionId, e.authorId, e.circleId, false, taskId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理朋友圈自动化时出错");
            }
        }

        /// <summary>
        /// 处理消息自动化 (自动回复与抢红包)
        /// </summary>
        public async Task HandleMessageAutomation(long accountId, string connectionId, string weChatId, string friendId, string contentXml, int contentType)
        {
             try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var account = await db.GetWechatAccount(accountId);
                    if (account != null && !string.IsNullOrEmpty(account.settings))
                    {
                        var settings = JsonSerializer.Deserialize<WechatAccountSettings>(account.settings);
                        if (settings != null)
                        {
                            // 1. 自动抢红包
                            if (settings.AutoAcceptLuckyMoney)
                            {
                                if (contentXml.Contains("<nativeurl>") && contentXml.Contains("hongbao")) 
                                {
                                     _logger.LogInformation("自动领取红包: 来自 {Friend}, 账号 {WeChat}", friendId, weChatId);
                                     string nativeUrl = ExtractXmlValue(contentXml, "nativeurl");
                                     string key = ExtractXmlValue(contentXml, "ver");
                                     if (string.IsNullOrEmpty(key)) key = nativeUrl;
                                     await _clientTaskService.SendTakeLuckyMoneyTaskAsync(connectionId, weChatId, friendId, 0, key);
                                }
                            }

                            // 2. 自动回复
                            if (!string.IsNullOrEmpty(settings.AutoReplyContent))
                            {
                                if (!string.IsNullOrEmpty(friendId) && !friendId.Contains("@chatroom"))
                                {
                                     // 仅对文本、图片、视频消息尝试回复
                                     if (contentType == 1 || contentType == 3 || contentType == 43) 
                                     {
                                         _logger.LogInformation("自动回复好友 {Friend}, 账号 {WeChat}", friendId, weChatId);
                                         await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, friendId, settings.AutoReplyContent);
                                     }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息自动化时出错");
            }
        }

        /// <summary>
        /// 简单的 XML 节点值提取
        /// </summary>
        private string ExtractXmlValue(string xml, string key)
        {
             try
            {
                string updatedKey = key;
                if (!updatedKey.EndsWith(">")) updatedKey = "<" + updatedKey + ">";
                string endKey = updatedKey.Replace("<", "</");

                int start = xml.IndexOf(updatedKey);
                if (start == -1) return "";
                start += updatedKey.Length;
                int end = xml.IndexOf(endKey, start);
                if (end == -1) return "";
                return xml.Substring(start, end - start);
            }
            catch { return ""; }
        }

        public override void Dispose()
        {
            _messageSub?.Dispose();
            _friendRequestSub?.Dispose();
            _circleSub?.Dispose();
            base.Dispose();
        }
    }
}
