using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // For IServiceScopeFactory
using SCRM.Services.Events;
using SCRM.API.Services.Data;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using SCRM.API.Models.Events;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SCRM.Shared.Core; // For Utility if needed, or just standard logging

namespace SCRM.Services.Automation
{
    public class AutomationService : BackgroundService
    {
        private readonly IEventBus _eventBus;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ClientTaskService _clientTaskService;
        private readonly ILogger<AutomationService> _logger;
        
        // Subscription tokens
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

            // 1. 订阅聊天消息 (自动回复 & 抢红包)
            // 注意: MessageReceivedEvent 包含 DTO "Message" 和 "DeviceUuid".
            // 然而，原始的 MessageRouter 逻辑需要访问 "XML 内容" 来处理红包.
            // 目前 MessageReceivedEvent 发送的 `msgDto` 是 { FriendId, Content, IsSelf }.
            // 如果 Content 只是 .ToStringUtf8()，我们仍然可以解析它.
            // 但是: 我们需要 AccountId. MessageReceivedEvent 有 DeviceUuid. ConnectionId?
            // EventForwardingService 使用 DeviceUuid 转发到 SignalR 组.
            // AutomationService 需要 AccountId 来查询数据库设置.
            // 之前的 Router 逻辑可以直接访问 ConnectionId 和 AccountId.
            // 为了稳健起见，我们坚持由 Router 传递我们需要的数据.
            // 实际上，MessageReceivedEvent 的 `Message` 属性是一个灵活的对象.
            // 但为了在这里进行类型化处理，我们可能需要更严格的事件，或者如果我们知道类型就直接转换.
            // 或者更好的是: 如果 MessageReceivedEvent 是为 UI 转发设计的，就不要复用它.
            // 我们创建 `AutomationMessageEvent` 或类似的事件?
            // 或者直接向 `MessageReceivedEvent` 添加必要的字段.
            
            _messageSub = _eventBus.Subscribe<MessageReceivedEvent>(async (e) =>
            {
               // 此事件主要用于 UI.
               // 在这里重新实现自动回复需要解析 `e.Message`.
               // 假设 e.Message 是匿名类型 { FriendId, Content, IsSelf }.
               // 我们可以使用反射或 dynamic.
               try 
               {
                   dynamic msg = e.Message;
                   string friendId = msg.FriendId;
                   string content = msg.Content;
                   bool isSelf = msg.IsSelf;

                   if (isSelf) return;

                   // 我们需要 AccountId 来查找设置.
                   // 之前的 Router 逻辑有它. e.OwnerId 是拥有者 (User), 不是微信 AccountId ?
                   // 等等, e.DeviceUuid 通常是 WeChatId (或者 IMEI).
                   // 我们可以通过 DeviceUuid 查找 `WechatAccount`?
                   // 有点复杂.
                   // 也许 MessageRouter 应该在事件中传递 AccountId.
               }
               catch(Exception ex) { _logger.LogError(ex, "自动化服务处理消息事件出错"); }
            });

            // *简化策略*:
            // 目前，我们实现好友请求和朋友圈的具体处理程序.
            // 对于消息 (自动回复/红包)，因为逻辑较"重"，我们定义一个 `AutomationMessageEvent` 显式携带 `AccountId`.
            
            _friendRequestSub = _eventBus.Subscribe<FriendRequestEvent>(async (e) =>
            {
                await HandleFriendRequest(e);
            });

            _circleSub = _eventBus.Subscribe<CircleNewPublishEvent>(async (e) =>
            {
                await HandleCircleNewPublish(e);
            });
            
            _eventBus.Subscribe<AutomationMessageEvent>(async (e) =>
            {
                await HandleMessageAutomation(e.AccountId, e.ConnectionId, e.WeChatId, e.FriendId, e.ContentXml, e.ContentType);
            });
            
            return Task.CompletedTask;
        }

        private async Task HandleFriendRequest(FriendRequestEvent e)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var account = await db.GetWechatAccount(e.AccountId);
                    if (account != null && !string.IsNullOrEmpty(account.Settings))
                    {
                        var settings = JsonSerializer.Deserialize<WechatAccountSettings>(account.Settings);
                        if (settings != null && settings.AutoAcceptFriendRequest)
                        {
                            _logger.LogInformation("Auto-Accepting Friend Request: {Friend} for Account {WeChat}", e.FriendNick, e.WeChatId);
                            long taskId = DateTime.UtcNow.Ticks;
                            await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(e.ConnectionId, e.FriendId, e.FriendNick, taskId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling friend request automation");
            }
        }

        private async Task HandleCircleNewPublish(CircleNewPublishEvent e)
        {
             try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var account = await db.GetWechatAccount(e.AccountId);
                    if (account != null && !string.IsNullOrEmpty(account.Settings))
                    {
                        var settings = JsonSerializer.Deserialize<WechatAccountSettings>(account.Settings);
                        if (settings != null && settings.AutoLikeMoments)
                        {
                            _logger.LogInformation("Auto-Liking Moment {CircleId} for Account {WeChat}", e.CircleId, e.WeChatId);
                            long taskId = DateTime.UtcNow.Ticks;
                            // Note: Passing AuthorId as the 'WeChatId' param for the task?
                            // Based on previous analysis:
                            await _clientTaskService.SendCircleLikeTaskAsync(e.ConnectionId, e.AuthorId, e.CircleId, false, taskId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling circle automation");
            }
        }

        // To be called by a new event subscription
        public async Task HandleMessageAutomation(long accountId, string connectionId, string weChatId, string friendId, string contentXml, int contentType)
        {
             try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var account = await db.GetWechatAccount(accountId);
                    if (account != null && !string.IsNullOrEmpty(account.Settings))
                    {
                        var settings = JsonSerializer.Deserialize<WechatAccountSettings>(account.Settings);
                        if (settings != null)
                        {
                            // 1. Auto Accept Lucky Money
                            if (settings.AutoAcceptLuckyMoney)
                            {
                                if (contentXml.Contains("<nativeurl>") && contentXml.Contains("hongbao")) 
                                {
                                     _logger.LogInformation("Auto-Accepting RedPacket from {Friend} for {WeChat}", friendId, weChatId);
                                     string nativeUrl = ExtractXmlValue(contentXml, "nativeurl");
                                     string key = ExtractXmlValue(contentXml, "ver");
                                     if (string.IsNullOrEmpty(key)) key = nativeUrl;
                                     await _clientTaskService.SendTakeLuckyMoneyTaskAsync(connectionId, weChatId, friendId, 0, key);
                                }
                            }

                            // 2. Auto Reply
                            if (!string.IsNullOrEmpty(settings.AutoReplyContent))
                            {
                                if (!string.IsNullOrEmpty(friendId) && !friendId.Contains("@chatroom"))
                                {
                                     // EnumContentType check (1, 3, 43 etc)
                                     if (contentType == 1 || contentType == 3 || contentType == 43) 
                                     {
                                         _logger.LogInformation("Auto-Replying to {Friend} for {WeChat}", friendId, weChatId);
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
                _logger.LogError(ex, "Error handling message automation");
            }
        }

        private string ExtractXmlValue(string xml, string key)
        {
            // Simple util method, can be duplicated or static util
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
    }
}
