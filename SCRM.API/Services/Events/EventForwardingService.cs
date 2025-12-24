using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCRM.API.Hubs;
using System.Threading;
using System.Threading.Tasks;
using SCRM.API.Models.Events;

namespace SCRM.Services.Events
{
    public class EventForwardingService : BackgroundService
    {
        private readonly IEventBus _eventBus;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly ILogger<EventForwardingService> _logger;
        private IDisposable? _deviceConnectedSubscription;
        private IDisposable? _messageReceivedSubscription;

        public EventForwardingService(IEventBus eventBus, IHubContext<ClientHub> hubContext, ILogger<EventForwardingService> logger)
        {
            _eventBus = eventBus;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Event Forwarding Service started.");

            // 订阅设备连接事件
            _deviceConnectedSubscription = _eventBus.Subscribe<DeviceConnectedEvent>(async (e) =>
            {
                if (!string.IsNullOrEmpty(e.OwnerId))
                {
                    _logger.LogInformation("Forwarding DeviceConnectedEvent for {UserId} to Owner {OwnerId}", e.UserId, e.OwnerId);
                    
                    // 通过 SignalR 推送 "DeviceConnectionChanged" 消息给对应的主人
                    // 参数: DeviceId, ConnectionId, IsOnline
                    await _hubContext.Clients.User(e.OwnerId).SendAsync("DeviceConnectionChanged", e.UserId, e.ConnectionId, true, stoppingToken);
                }
            });

            // 订阅消息接收事件 (MessageReceivedEvent)
            // 当 Netty收到消息后触发此事件，本服务负责将其转发给 SignalR
            _messageReceivedSubscription = _eventBus.Subscribe<MessageReceivedEvent>(async (e) =>
            {
                if (!string.IsNullOrEmpty(e.DeviceUuid) && e.Message != null)
                {
                    _logger.LogInformation("转发消息事件: 设备 {DeviceUuid} -> SignalR组 {DeviceUuid}", e.DeviceUuid, e.DeviceUuid);
                    
                    // 通过 SignalR 推送 "ReceiveMessage" 消息给订阅了该设备组的网页客户端
                    // e.Message 是一个包含消息内容的 DTO 对象
                    // 网页端 Chat.razor 必须监听 "ReceiveMessage" 并接收此对象
                    await _hubContext.Clients.Group(e.DeviceUuid).SendAsync("ReceiveMessage", e.Message, stoppingToken);
                }
            });

            // 订阅联系人列表更新事件
            _eventBus.Subscribe<ContactsReceivedEvent>(async (e) =>
            {
                if (!string.IsNullOrEmpty(e.DeviceUuid))
                {
                    // Push to SignalR group (DeviceUuid)
                    // 前端收到 "ContactsUpdated" 信号后，应该重新调用 GetContacts API
                    await _hubContext.Clients.Group(e.DeviceUuid).SendAsync("ContactsUpdated", e.AccountId, stoppingToken);
                    _logger.LogInformation("转发联系人列表更新事件: 设备 {DeviceUuid} -> SignalR组 {DeviceUuid}", e.DeviceUuid, e.DeviceUuid);
                }
            });

            // 订阅任务结果事件 (TaskResultReceivedEvent)
            _eventBus.Subscribe<TaskResultReceivedEvent>(async (e) =>
            {
                if (!string.IsNullOrEmpty(e.DeviceUuid))
                {
                    _logger.LogInformation("转发任务结果: TaskId={TaskId}, Success={Success} -> SignalR组 {DeviceUuid}", e.TaskId, e.Success, e.DeviceUuid);
                    // 使用匿名对象发送，确保前端能收到 TaskId
                    await _hubContext.Clients.Group(e.DeviceUuid).SendAsync("OnTaskResult", new { TaskId = e.TaskId, Success = e.Success, Message = e.Message }, stoppingToken);
                }
            });

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _deviceConnectedSubscription?.Dispose();
            _messageReceivedSubscription?.Dispose();
            base.Dispose();
        }
    }
}
