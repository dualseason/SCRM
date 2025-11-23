using Microsoft.AspNetCore.Mvc;
using SCRM.Services;
using System.Threading.Tasks;
using Jubo.JuLiao.IM.Wx.Proto;

namespace SCRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientTaskController : ControllerBase
    {
        private readonly ClientTaskService _clientTaskService;
        private readonly ConnectionManager _connectionManager;

        public ClientTaskController(ClientTaskService clientTaskService, ConnectionManager connectionManager)
        {
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
        }

        [HttpGet("connections")]
        public async Task<IActionResult> GetConnections()
        {
            var connections = await _connectionManager.GetAllConnectionsAsync();
            return Ok(connections);
        }

        [HttpPost("heartbeat/{connectionId}")]
        public async Task<IActionResult> SendHeartBeat(string connectionId)
        {
            var result = await _clientTaskService.SendHeartBeatAsync(connectionId);
            return Ok(new { success = result });
        }

        [HttpPost("sync-friends/{connectionId}")]
        public async Task<IActionResult> SendSyncFriends(string connectionId)
        {
            var result = await _clientTaskService.SendSyncFriendListTaskAsync(connectionId);
            return Ok(new { success = result });
        }

        [HttpPost("talk-to-friend")]
        public async Task<IActionResult> SendTalkToFriend([FromBody] TalkToFriendRequest request)
        {
            var result = await _clientTaskService.SendTalkToFriendTaskAsync(
                request.ConnectionId, 
                request.FriendWxId, 
                request.Content);
            return Ok(new { success = result });
        }

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

    public class TalkToFriendRequest
    {
        public string ConnectionId { get; set; }
        public string FriendWxId { get; set; }
        public string Content { get; set; }
    }

    public class AddFriendRequest
    {
        public string ConnectionId { get; set; }
        public string FriendWxId { get; set; }
        public string Message { get; set; }
        public int Scene { get; set; } = 3;
    }
}
