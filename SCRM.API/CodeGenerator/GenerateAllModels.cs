using System.Text;

namespace SCRM.CodeGenerator
{
    /// <summary>
    /// 生成所有数据库表对应的Entity、DTO、Repository和Controller
    /// </summary>
    public class GenerateAllModels
    {
        private static readonly Dictionary<string, (string Name, string Comment)> Tables = new()
        {
            { "devices", ("Device", "设备信息表") },
            { "groups", ("Group", "群聊表") },
            { "wechat_accounts", ("WechatAccount", "微信账号信息表") },
            { "contact_groups", ("ContactGroup", "联系人分组表") },
            { "contact_tags", ("ContactTag", "联系人标签表") },
            { "contacts", ("Contact", "联系人表") },
            { "device_authorizations", ("DeviceAuthorization", "设备授权表") },
            { "device_heartbeats", ("DeviceHeartbeat", "设备心跳表") },
            { "device_version_logs", ("DeviceVersionLog", "设备版本日志表") },
            { "friend_detection_logs", ("FriendDetectionLog", "好友检测日志表") },
            { "friend_requests", ("FriendRequest", "好友请求表") },
            { "group_announcements", ("GroupAnnouncement", "群公告表") },
            { "group_change_logs", ("GroupChangeLog", "群聊变更日志表") },
            { "group_invitations", ("GroupInvitation", "群邀请表") },
            { "group_members", ("GroupMember", "群成员表") },
            { "group_message_sync_logs", ("GroupMessageSyncLog", "群消息同步日志表") },
            { "group_qrcodes", ("GroupQrcode", "群二维码表") },
            { "mass_messages", ("MassMessage", "群发消息表") },
            { "message_sync_logs", ("MessageSyncLog", "消息同步日志表") },
            { "messages", ("Message", "聊天消息表") },
            { "server_redirects", ("ServerRedirect", "服务器重定向表") },
            { "account_status_logs", ("AccountStatusLog", "账号状态日志表") },
            { "contact_change_logs", ("ContactChangeLog", "联系人变更日志表") },
            { "contact_group_relations", ("ContactGroupRelation", "联系人分组关系表") },
            { "contact_tag_relations", ("ContactTagRelation", "联系人标签关系表") },
            { "mass_message_details", ("MassMessageDetail", "群发消息详情表") },
            { "message_extensions", ("MessageExtension", "消息扩展表") },
            { "message_forwards", ("MessageForward", "消息转发表") },
            { "message_media", ("MessageMedia", "消息媒体表") },
            { "message_revocations", ("MessageRevocation", "消息撤回表") },
            { "voice_to_text_logs", ("VoiceToTextLog", "语音转文字日志表") },
            { "message_forward_details", ("MessageForwardDetail", "消息转发详情表") },
            { "moments_posts", ("MomentsPost", "朋友圈文章表") },
            { "moments_likes", ("MomentsLike", "朋友圈点赞表") },
            { "moments_comments", ("MomentsComment", "朋友圈评论表") },
            { "wallet_transactions", ("WalletTransaction", "钱包交易记录表") },
            { "red_packets", ("RedPacket", "红包发送记录表") },
            { "red_packet_records", ("RedPacketRecord", "红包领取记录表") },
            { "official_accounts", ("OfficialAccount", "公众号账号表") },
            { "miniprogram_accounts", ("MiniprogramAccount", "小程序账号表") },
            { "official_account_search_logs", ("OfficialAccountSearchLog", "公众号搜索记录表") },
            { "miniprogram_search_logs", ("MiniprogramSearchLog", "小程序搜索记录表") },
            { "official_account_messages", ("OfficialAccountMessage", "公众号消息记录表") },
            { "miniprogram_messages", ("MiniprogramMessage", "小程序消息记录表") },
            { "official_account_subscriptions", ("OfficialAccountSubscription", "公众号订阅记录表") },
            { "miniprogram_access_logs", ("MiniprogramAccessLog", "小程序访问记录表") },
            { "official_account_follow_logs", ("OfficialAccountFollowLog", "公众号关注事件日志表") },
            { "miniprogram_follow_logs", ("MiniprogramFollowLog", "小程序关注事件日志表") },
            { "conversations", ("Conversation", "会话管理表") },
            { "device_commands", ("DeviceCommand", "设备命令表") },
            { "device_status_logs", ("DeviceStatusLog", "设备状态日志表") },
            { "device_locations", ("DeviceLocation", "设备位置记录表") },
            { "system_notifications", ("SystemNotification", "系统通知表") },
            { "app_versions", ("AppVersion", "应用版本表") },
            { "roles", ("Role", "角色表") },
            { "permissions", ("Permission", "权限表") },
            { "user_roles", ("UserRole", "用户角色关系表") },
            { "role_permissions", ("RolePermission", "角色权限关系表") }
        };

        public static void Main(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            Console.WriteLine($"开始生成代码到: {basePath}");

            try
            {
                foreach (var (tableName, (className, comment)) in Tables)
                {
                    GenerateEntity(basePath, tableName, className, comment);
                    GenerateDto(basePath, tableName, className, comment);
                    GenerateRepository(basePath, className, comment);
                    GenerateController(basePath, className, comment);

                    Console.WriteLine($"✓ 生成完成: {className}");
                }

                Console.WriteLine("\n所有代码已成功生成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 生成失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void GenerateEntity(string basePath, string tableName, string className, string comment)
        {
            var code = $@"using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCRM.Models.Entities
{{
    /// <summary>
    /// {comment}
    /// </summary>
    [Table(""{tableName}"")]
    public class {className}
    {{
        /// <summary>主键ID</summary>
        [Key]
        public long Id {{ get; set; }}

        /// <summary>是否删除</summary>
        public bool IsDeleted {{ get; set; }} = false;

        /// <summary>创建时间</summary>
        public DateTime? CreatedAt {{ get; set; }} = DateTime.UtcNow;

        /// <summary>更新时间</summary>
        public DateTime? UpdatedAt {{ get; set; }} = DateTime.UtcNow;

        /// <summary>删除时间</summary>
        public DateTime? DeletedAt {{ get; set; }}
    }}
}}
";
            SaveFile(basePath, $"Models/Entities/{className}.cs", code);
        }

        private static void GenerateDto(string basePath, string tableName, string className, string comment)
        {
            var code = $@"using System.ComponentModel.DataAnnotations;

namespace SCRM.Models.Dtos
{{
    /// <summary>
    /// {comment} DTO
    /// </summary>
    public class {className}Dto
    {{
        /// <summary>主键ID</summary>
        [Required]
        public long Id {{ get; set; }}

        /// <summary>是否删除</summary>
        public bool IsDeleted {{ get; set; }} = false;

        /// <summary>创建时间</summary>
        public DateTime? CreatedAt {{ get; set; }}

        /// <summary>更新时间</summary>
        public DateTime? UpdatedAt {{ get; set; }}
    }}
}}
";
            SaveFile(basePath, $"Models/Dtos/{className}Dto.cs", code);
        }

        private static void GenerateRepository(string basePath, string className, string comment)
        {
            var repositoryInterface = $@"using SCRM.Core.Repository;
using SCRM.Models.Entities;

namespace SCRM.Services.Repository
{{
    /// <summary>
    /// {comment} 仓储接口
    /// </summary>
    public interface I{className}Repository : IBaseRepository<{className}, long>
    {{
        // 在此添加特定的业务方法
    }}
}}
";
            SaveFile(basePath, $"Services/Repository/I{className}Repository.cs", repositoryInterface);

            var repositoryImpl = $@"using SCRM.Core.Repository;
using SCRM.Models.Entities;
using SCRM.Services.Data;

namespace SCRM.Services.Repository
{{
    /// <summary>
    /// {comment} 仓储实现
    /// </summary>
    public class {className}Repository : BaseRepository<{className}, long>, I{className}Repository
    {{
        public {className}Repository(ApplicationDbContext context) : base(context)
        {{
        }}

        // 在此添加特定的业务逻辑实现
    }}
}}
";
            SaveFile(basePath, $"Services/Repository/{className}Repository.cs", repositoryImpl);
        }

        private static void GenerateController(string basePath, string className, string comment)
        {
            var controllerName = ToCamelCase(className);
            var code = $@"using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using SCRM.Core.Controllers;
using SCRM.Models.Dtos;
using SCRM.Models.Entities;
using SCRM.Services.Repository;

namespace SCRM.Controllers
{{
    /// <summary>
    /// {comment} 控制器
    /// </summary>
    [Authorize]
    [ApiController]
    [Route(""api/[controller]"")]
    [SwaggerTag(""{comment}"")]
    public class {className}Controller : BaseApiController<{className}Dto>
    {{
        private readonly I{className}Repository _{controllerName}Repository;

        public {className}Controller(I{className}Repository {controllerName}Repository)
        {{
            _{controllerName}Repository = {controllerName}Repository;
        }}

        /// <summary>
        /// 获取所有{comment}
        /// </summary>
        [HttpGet]
        [SwaggerOperation(Summary = ""获取所有{comment}"")]
        public async Task<IActionResult> GetAll()
        {{
            try
            {{
                var items = await _{controllerName}Repository.GetAllAsync();
                return OkResponse(items.Select(MapToDto).ToList(), ""获取成功"");
            }}
            catch (Exception ex)
            {{
                return ErrorResponse<List<{className}Dto>>(ex.Message);
            }}
        }}

        /// <summary>
        /// 根据ID获取{comment}
        /// </summary>
        [HttpGet(""{id}"")]
        [SwaggerOperation(Summary = ""根据ID获取{comment}"")]
        public async Task<IActionResult> GetById(long id)
        {{
            try
            {{
                var item = await _{controllerName}Repository.GetByIdAsync(id);
                if (item == null)
                    return NotFoundResponse<{className}Dto>(""记录不存在"");

                return OkResponse(MapToDto(item), ""获取成功"");
            }}
            catch (Exception ex)
            {{
                return ErrorResponse<{className}Dto>(ex.Message);
            }}
        }}

        /// <summary>
        /// 创建新的{comment}
        /// </summary>
        [HttpPost]
        [SwaggerOperation(Summary = ""创建新的{comment}"")]
        public async Task<IActionResult> Create([FromBody] {className}Dto dto)
        {{
            try
            {{
                if (!ModelState.IsValid)
                    return BadRequestResponse<{className}Dto>(""输入验证失败"");

                var entity = MapToEntity(dto);
                var result = await _{controllerName}Repository.AddAsync(entity);

                return Ok(new {{ Success = true, Data = MapToDto(result), Message = ""创建成功"" }});
            }}
            catch (Exception ex)
            {{
                return ErrorResponse<{className}Dto>(ex.Message);
            }}
        }}

        /// <summary>
        /// 更新{comment}
        /// </summary>
        [HttpPut(""{id}"")]
        [SwaggerOperation(Summary = ""更新{comment}"")]
        public async Task<IActionResult> Update(long id, [FromBody] {className}Dto dto)
        {{
            try
            {{
                if (!ModelState.IsValid)
                    return BadRequestResponse<{className}Dto>(""输入验证失败"");

                var entity = await _{controllerName}Repository.GetByIdAsync(id);
                if (entity == null)
                    return NotFoundResponse<{className}Dto>(""记录不存在"");

                MapToEntity(dto, entity);
                var result = await _{controllerName}Repository.UpdateAsync(entity);

                return OkResponse(MapToDto(result), ""更新成功"");
            }}
            catch (Exception ex)
            {{
                return ErrorResponse<{className}Dto>(ex.Message);
            }}
        }}

        /// <summary>
        /// 删除{comment}
        /// </summary>
        [HttpDelete(""{id}"")]
        [SwaggerOperation(Summary = ""删除{comment}"")]
        public async Task<IActionResult> Delete(long id)
        {{
            try
            {{
                var result = await _{controllerName}Repository.DeleteAsync(id);
                if (!result)
                    return NotFoundResponse<object>(""记录不存在"");

                return OkResponse<object>(null, ""删除成功"");
            }}
            catch (Exception ex)
            {{
                return ErrorResponse<object>(ex.Message);
            }}
        }}

        /// <summary>
        /// 分页获取{comment}
        /// </summary>
        [HttpGet(""page"")]
        [SwaggerOperation(Summary = ""分页获取{comment}"")]
        public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {{
            try
            {{
                var (items, total) = await _{controllerName}Repository.GetPagedAsync(pageNumber, pageSize);
                return OkPagedResponse(pageNumber, pageSize, total, items.Select(MapToDto).ToList());
            }}
            catch (Exception ex)
            {{
                return ErrorResponse<PagedResponse<{className}Dto>>(ex.Message);
            }}
        }}

        private {className}Dto MapToDto({className} entity)
        {{
            return new {className}Dto
            {{
                Id = entity.Id,
                IsDeleted = entity.IsDeleted,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            }};
        }}

        private {className} MapToEntity({className}Dto dto, {className}? entity = null)
        {{
            entity ??= new {className}();
            entity.IsDeleted = dto.IsDeleted;
            entity.UpdatedAt = DateTime.UtcNow;
            return entity;
        }}
    }}
}}
";
            SaveFile(basePath, $"Controllers/{className}Controller.cs", code);
        }

        private static void SaveFile(string basePath, string relativePath, string content)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, content, Encoding.UTF8);
        }

        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return char.ToLower(input[0]) + input[1..];
        }
    }
}
