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

        [HttpPost("fileUpload")]
        public async Task<IActionResult> fileUpload(
            IFormFile myfile,
            [FromForm] string packageName,
            [FromForm] string device)
        {
            try
            {
                if (myfile == null || myfile.Length == 0)
                    return BadRequest(new { code = -1, message = "No file uploaded" });

                // Get configuration or default
                var storePath = _config["FileUploadSettings:StorePath"];
                if (string.IsNullOrEmpty(storePath)) storePath = "wwwroot/uploads";

                string uploadPath;
                if (Path.IsPathRooted(storePath))
                {
                    uploadPath = storePath;
                }
                else
                {
                    // If relative, assume relative to ContentRoot (Project Root), not wwwroot unless specified
                    // But for "wwwroot/uploads" it works fine relative to ContentRoot
                    uploadPath = Path.Combine(_env.ContentRootPath, storePath);
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
                var fileUrl = $"{baseUrl}/{requestPrefix}/{fileName}";

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
