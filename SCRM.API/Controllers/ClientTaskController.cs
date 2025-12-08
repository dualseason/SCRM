using Microsoft.AspNetCore.Mvc;
using SCRM.Services;
using System.Threading.Tasks;
using SCRM.SHARED.Proto;

namespace SCRM.Controllers
{
    /// <summary>
    /// 客户端任务控制器 - 管理微信客户端的各种操作任务
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ClientTaskController : ControllerBase
    {
        private readonly ClientTaskService _clientTaskService;
        private readonly ConnectionManager _connectionManager;

        public ClientTaskController(ClientTaskService clientTaskService, ConnectionManager connectionManager)
        {
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// 获取所有活跃的客户端连接信息
        /// </summary>
        /// <returns>连接列表</returns>
        /// <response code="200">成功获取连接列表</response>
        [HttpGet("connections")]
        public async Task<IActionResult> GetConnections()
        {
            var connections = await _connectionManager.GetAllConnectionsAsync();
            return Ok(connections);
        }

        /// <summary>
        /// 向指定连接发送心跳包
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <returns>发送结果</returns>
        /// <response code="200">心跳发送成功</response>
        [HttpPost("heartbeat/{connectionId}")]
        public async Task<IActionResult> SendHeartBeat(string connectionId)
        {
            var result = await _clientTaskService.SendHeartBeatAsync(connectionId);
            return Ok(new { success = result });
        }

        /// <summary>
        /// 同步指定客户端的好友列表
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <returns>同步任务结果</returns>
        /// <response code="200">同步任务发送成功</response>
        [HttpPost("sync-friends/{connectionId}")]
        public async Task<IActionResult> SendSyncFriends(string connectionId)
        {
            var result = await _clientTaskService.SendSyncFriendListTaskAsync(connectionId);
            return Ok(new { success = result });
        }

        /// <summary>
        /// 向好友发送消息
        /// </summary>
        /// <param name="request">发送消息请求</param>
        /// <returns>发送结果</returns>
        /// <response code="200">消息发送成功</response>
        [HttpPost("talk-to-friend")]
        public async Task<IActionResult> SendTalkToFriend([FromBody] TalkToFriendRequest request)
        {
            var result = await _clientTaskService.SendTalkToFriendTaskAsync(
                request.ConnectionId,
                request.FriendWxId,
                request.Content);
            return Ok(new { success = result });
        }

        /// <summary>
        /// 添加好友请求
        /// </summary>
        /// <param name="request">添加好友请求</param>
        /// <returns>添加结果</returns>
        /// <response code="200">添加请求发送成功</response>
        [HttpPost("add-friend")]
        public async Task<IActionResult> SendAddFriend([FromBody] AddFriendRequest request)
        {
            var result = await _clientTaskService.SendAddFriendTaskAsync(
                request.ConnectionId,
                request.FriendWxId,
                request.Message,
                request.Scene);
            return Ok(new { success = result });
        }
    }

    /// <summary>
    /// 发送消息给好友的请求模型
    /// </summary>
    public class TalkToFriendRequest
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        /// <example>conn_123456</example>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// 好友微信ID
        /// </summary>
        /// <example>friend_wxid_123</example>
        public string FriendWxId { get; set; } = string.Empty;

        /// <summary>
        /// 消息内容
        /// </summary>
        /// <example>你好，这是一条测试消息</example>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// 添加好友的请求模型
    /// </summary>
    public class AddFriendRequest
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        /// <example>conn_123456</example>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// 好友微信ID
        /// </summary>
        /// <example>friend_wxid_456</example>
        public string FriendWxId { get; set; } = string.Empty;

        /// <summary>
        /// 验证消息
        /// </summary>
        /// <example>我是通过扫码添加您的好友</example>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 添加场景（默认为3，通过扫码添加）
        /// </summary>
        /// <example>3</example>
        public int Scene { get; set; } = 3;
    }
}
