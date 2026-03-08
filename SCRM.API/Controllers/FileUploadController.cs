using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using SCRM.SHARED.Models.Events;

namespace SCRM.API.Controllers
{
    [ApiController]
    [Route("")]
    [Microsoft.AspNetCore.Authorization.Authorize]
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

        [HttpPost("fileUpload")]
        public async Task<IActionResult> fileUpload(
            IFormFile myfile,
            [FromForm] string packageName,
            [FromForm] string device,
            [FromForm] string? bizType,
            [FromForm] string? wechatId)
        {
            try
            {
                if (myfile == null || myfile.Length == 0)
                    return BadRequest(new { code = -1, message = "No file uploaded" });

                // Get configuration or default
                var storePath = _config["FileUploadSettings:StorePath"];
                if (string.IsNullOrEmpty(storePath)) storePath = "wwwroot/uploads";

                string subDir = "";
                if (bizType == "screenshot" || bizType == "log")
                {
                    if (!string.IsNullOrEmpty(device))
                        subDir = $"devices/{SanitizePathToken(device)}/{bizType}";
                }
                else if (!string.IsNullOrEmpty(wechatId))
                {
                    string subFolder = bizType switch
                    {
                        "avatar" => "pic",
                        "chat_pic" => "pic",
                        "voice" => "voice",
                        _ => "files"
                    };
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
                if (!string.IsNullOrEmpty(device))
                {
                   _eventPublisher.PublishEvent("OnScreenShotUploaded", new ScreenShotUploadedEvent(fileUrl, device));
                   _logger.LogInformation($"[Push] Published OnScreenShotUploaded event for {device}");
                }

                return Ok(new
                {
                    businessCode = 0,
                    msg = "success",
                    responseData = new
                    {
                        url = fileUrl
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");
                return StatusCode(500, new { businessCode = -1, msg = ex.Message });
            }
        }
    }
}
