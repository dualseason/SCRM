using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using SCRM.SHARED.Models.Events;
using System.Linq;

namespace SCRM.API.Controllers
{
    [ApiController]
    [Route("")]
        [AllowAnonymous]
        public class FileUploadController : ControllerBase
        {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileUploadController> _logger;
        private readonly IConfiguration _config;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<SCRM.API.Hubs.ClientHub> _hubContext;
        private readonly SCRM.Shared.Interfaces.ICrmEventPublisher _eventPublisher;

        public FileUploadController(
            IWebHostEnvironment env, 
            ILogger<FileUploadController> logger, 
            IConfiguration config,
            Microsoft.AspNetCore.SignalR.IHubContext<SCRM.API.Hubs.ClientHub> hubContext,
            SCRM.Shared.Interfaces.ICrmEventPublisher eventPublisher)
        {
            _env = env;
            _logger = logger;
            _config = config;
            _hubContext = hubContext;
            _eventPublisher = eventPublisher;
        }

        private string SanitizePathToken(string input)
        {
            if (string.IsNullOrEmpty(input)) return "unknown";
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var ch in invalidChars)
            {
                input = input.Replace(ch, '_');
            }
            return input.Replace("..", "_").Replace("/", "_").Replace("\\", "_");
        }

        /// <summary>
        /// 兼容两类上传身份：
        /// 1. Web 端正常 JWT 登录用户；
        /// 2. Android 客户端携带 X-App-Secret / X-API-Key。
        /// 
        /// 背景：SmRun 文件上传链长期沿用客户端 API Key，而不是统一走 JWT。
        /// 若这里只接受 JWT，会在截图/素材上传时直接掉到 4xx。
        /// </summary>
        private bool TryAuthorizeUpload(out IActionResult? errorResult)
        {
            errorResult = null;

            if (User?.Identity?.IsAuthenticated == true)
            {
                return true;
            }

            var configuredApiKey = _config["SecuritySettings:ClientApiKey"];
            var providedApiKey = Request.Headers["X-App-Secret"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedApiKey))
            {
                providedApiKey = Request.Headers["X-API-Key"].FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                _logger.LogWarning("FileUpload rejected: SecuritySettings:ClientApiKey 未配置。");
                errorResult = UploadError(StatusCodes.Status401Unauthorized, -1, "Upload auth config missing");
                return false;
            }

            if (!string.Equals(configuredApiKey, providedApiKey, StringComparison.Ordinal))
            {
                _logger.LogWarning("FileUpload rejected: API Key invalid. Remote={RemoteIp}", HttpContext.Connection.RemoteIpAddress);
                errorResult = UploadError(StatusCodes.Status401Unauthorized, -1, "Unauthorized upload client");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 返回同时兼容 SmRun 旧模型与 62203 原版模型的上传响应。
        /// SmRun 旧模型读取 businessCode / responseData / message；
        /// 62203 原版模型读取 bizCode / data / msg。
        /// </summary>
        private IActionResult UploadOk(string fileUrl, long fileSize)
        {
            var data = new
            {
                url = fileUrl,
                fileUrl,
                downloadUrl = fileUrl,
                fileSize,
                size = fileSize,
                length = fileSize
            };

            return Ok(new
            {
                businessCode = 0,
                bizCode = 0,
                code = 0,
                msg = "success",
                message = "success",
                responseData = data,
                data
            });
        }

        /// <summary>
        /// 上传失败时同样返回新旧字段，便于安卓端稳定提取错误原因。
        /// </summary>
        private ObjectResult UploadError(int httpStatusCode, int businessCode, string message)
        {
            return StatusCode(httpStatusCode, new
            {
                businessCode,
                bizCode = businessCode,
                code = businessCode,
                msg = message,
                message,
                responseData = (object?)null,
                data = (object?)null
            });
        }

        [HttpPost("fileUpload")]
        [HttpPost("fileUpUrl")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(209715200)]
        [RequestFormLimits(MultipartBodyLengthLimit = 209715200)]
        public async Task<IActionResult> fileUpload()
        {
            try
            {
                if (!TryAuthorizeUpload(out var authError))
                {
                    return authError!;
                }

                IFormCollection form;
                try
                {
                    // 手动读取 multipart，避免 [ApiController] 在 Action 进入前直接返回空 body 的 400。
                    // 这样即使安卓端再次传出异常文件段，也能在服务端日志和响应体中看到明确原因。
                    form = await Request.ReadFormAsync();
                }
                catch (Exception ex) when (ex is BadHttpRequestException || ex is InvalidDataException || ex is IOException)
                {
                    _logger.LogWarning(ex,
                        "FileUpload rejected: invalid multipart form. ContentType={ContentType}, ContentLength={ContentLength}, Remote={RemoteIp}",
                        Request.ContentType, Request.ContentLength, HttpContext.Connection.RemoteIpAddress);
                    return UploadError(StatusCodes.Status400BadRequest, -1, $"Invalid multipart form: {ex.Message}");
                }

                string? packageName = form["packageName"].FirstOrDefault();
                string? device = form["device"].FirstOrDefault();
                string? bizType = form["bizType"].FirstOrDefault();
                string? wechatId = form["wechatId"].FirstOrDefault();
                IFormFile? myfile = form.Files.GetFile("myfile")
                                    ?? form.Files.GetFile("file")
                                    ?? form.Files.GetFile("uploadFile")
                                    ?? form.Files.FirstOrDefault();

                packageName ??= string.Empty;
                device ??= string.Empty;

                if (myfile == null || myfile.Length == 0)
                {
                    _logger.LogWarning("FileUpload rejected: no file part. ContentType={ContentType}, FormFiles={FormFiles}",
                        Request.ContentType, form.Files.Count);
                    return UploadError(StatusCodes.Status400BadRequest, -1, "No file uploaded");
                }

                _logger.LogInformation("FileUpload accepted: device={Device}, bizType={BizType}, wechatId={WeChatId}, fileName={FileName}, size={FileSize}",
                    device, bizType, wechatId, myfile.FileName, myfile.Length);

                // Get configuration or default
                var storePath = _config["FileUploadSettings:StorePath"];
                if (string.IsNullOrEmpty(storePath)) storePath = "wwwroot/uploads";

                string subDir = "";
                var normalizedBizType = NormalizeBizType(bizType);
                if (normalizedBizType == "screenshot" || normalizedBizType == "log")
                {
                    if (!string.IsNullOrEmpty(device))
                        subDir = $"devices/{SanitizePathToken(device)}/{normalizedBizType}";
                }
                else if (!string.IsNullOrEmpty(wechatId))
                {
                    string subFolder = ResolveWechatUploadSubFolder(bizType);
                    subDir = $"wx/{SanitizePathToken(wechatId)}/{subFolder}";
                }

                string uploadPath;
                if (Path.IsPathRooted(storePath))
                {
                    uploadPath = string.IsNullOrEmpty(subDir) ? storePath : Path.Combine(storePath, subDir);
                }
                else
                {
                    // If relative, assume relative to ContentRoot (Project Root), not wwwroot unless specified
                    // But for "wwwroot/uploads" it works fine relative to ContentRoot
                    uploadPath = string.IsNullOrEmpty(subDir) 
                        ? Path.Combine(_env.ContentRootPath, storePath)
                        : Path.Combine(_env.ContentRootPath, storePath, subDir);
                }

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                // Generate safe filename
                var extension = Path.GetExtension(myfile.FileName);
                var fileName = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await myfile.CopyToAsync(stream);
                }

                // Generate URL
                var requestPrefix = _config["FileUploadSettings:RequestUrlPrefix"] ?? "uploads";
                requestPrefix = requestPrefix.Trim('/');
                
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                
                string urlPathContent = string.IsNullOrEmpty(subDir) 
                    ? $"{requestPrefix}/{fileName}" 
                    : $"{requestPrefix}/{subDir}/{fileName}";
                urlPathContent = urlPathContent.Replace("\\", "/");
                
                var fileUrl = $"{baseUrl}/{urlPathContent}";

                _logger.LogInformation($"File uploaded: {fileName} from {device} to {filePath}");

                // [KISS] Unified Push Notification via EventBus
                // The Controller only publishes the event. 
                // 1. EventForwardingService will pick it up and push to SignalR Clients (Remote).
                // 2. CrmStore will pick it up and update Blazor UI (Local).
                if (!string.IsNullOrEmpty(device) && IsScreenshotUploadBizType(bizType))
                {
                   _eventPublisher.PublishEvent("OnScreenShotUploaded", new ScreenShotUploadedEvent(fileUrl, device));
                   _logger.LogInformation($"[Push] Published OnScreenShotUploaded event for {device}");
                }

                return UploadOk(fileUrl, myfile.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");
                return UploadError(StatusCodes.Status500InternalServerError, -1, ex.Message);
            }
        }

        /// <summary>
        /// 根据 Android 上传时携带的 bizType 归档微信素材目录。
        /// <para>SmRun 聊天图片使用 chat_pic，聊天视频缩略图/视频链路使用 video，语音使用 voice；未知类型统一落到 files。</para>
        /// </summary>
        private static string ResolveWechatUploadSubFolder(string? bizType)
        {
            var normalized = NormalizeBizType(bizType);
            return normalized switch
            {
                "avatar" or "chat_pic" or "pic" or "image" or "photo" => "pic",
                "voice" or "chat_voice" or "audio" => "voice",
                "video" or "chat_video" or "video_thumb" or "thumb_video" => "video",
                "file" or "files" or "chat_file" => "files",
                _ => "files"
            };
        }

        /// <summary>
        /// 统一归一化上传业务类型，避免大小写或首尾空格导致视频/截图归档分支漂移。
        /// </summary>
        private static string? NormalizeBizType(string? bizType)
        {
            return bizType?.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// 只有真正的截图上传才发布截图事件。
        /// <para>Android 视频缩略图也会携带 device，但不能误推成 OnScreenShotUploaded。</para>
        /// </summary>
        private static bool IsScreenshotUploadBizType(string? bizType)
        {
            return NormalizeBizType(bizType) == "screenshot";
        }
    }
}
