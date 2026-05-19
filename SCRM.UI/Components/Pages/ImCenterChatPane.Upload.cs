using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;

namespace SCRM.UI.Components.Pages;

public partial class ImCenterChatPane
{
    #region 输入、上传、发送与滚动

    private void ToggleEmojiPicker()
    {
        _showEmojiPicker = !_showEmojiPicker;
    }

    private void AppendEmoji(string emoji)
    {
        _msgContent = $"{_msgContent}{emoji}";
        _showEmojiPicker = false;
    }

    private async Task OpenImagePicker()
    {
        await ClickHiddenFileInput(_imageInputFile, "im-chat-image-file");
    }

    private async Task OpenVideoPicker()
    {
        await ClickHiddenFileInput(_videoInputFile, "im-chat-video-file");
    }

    private async Task OpenAnyFilePicker()
    {
        await ClickHiddenFileInput(_anyInputFile, "im-chat-any-file");
    }

    private async Task ClickHiddenFileInput(InputFile? inputFile, string elementId)
    {
        if (!EnsureCanSendMedia())
        {
            return;
        }

        try
        {
            // Blazor Server 中 InputFile 的 ElementReference 偶尔指向包装节点；
            // 先尝试组件引用，再始终尝试 id 兜底，避免按钮点击无反应。
            var opened = false;
            if (inputFile != null)
            {
                opened = await JS.InvokeAsync<bool>("scrmClickElement", inputFile.Element);
            }

            if (!opened)
            {
                opened = await JS.InvokeAsync<bool>("scrmClickElementById", elementId);
            }

            if (!opened)
            {
                NoticeService.Notify(NotificationSeverity.Warning, "文件选择器未打开", "浏览器没有找到隐藏的上传控件，请刷新网页后重试。", duration: 4000);
            }
        }
        catch (JSDisconnectedException)
        {
            // 页面切换或浏览器断开时忽略，避免服务端日志出现大量无业务意义的 JSException。
        }
        catch (JSException ex)
        {
            NoticeService.Notify(NotificationSeverity.Error, "文件选择器打开失败", ex.Message);
        }
        catch (Exception ex)
        {
            NoticeService.Notify(NotificationSeverity.Error, "文件选择器打开失败", ex.Message);
        }
    }

    private async Task RefreshCurrentConversation()
    {
        if (Store.SelectedConversation != null)
        {
            await Store.SelectConversationAsync(Store.SelectedConversation);
            NoticeService.Notify(NotificationSeverity.Success, "已刷新", "当前会话消息已重新加载。");
            return;
        }

        NoticeService.Notify(NotificationSeverity.Warning, "无法刷新", "请先选择会话。");
    }

    private async Task UploadAndSendImagesAsync(InputFileChangeEventArgs args)
    {
        if (!EnsureCanSendMedia())
        {
            return;
        }

        var files = args.GetMultipleFiles(9).ToList();
        if (!files.Any())
        {
            return;
        }

        if (files.Any(file => file.Size <= 0 || file.Size > MaxChatImageSize || !IsContentType(file, "image/")))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "图片无效", "请选择 20MB 以内的图片文件。");
            return;
        }

        WarnIfUploadBaseUrlMayBeUnreachable();

        var confirm = await ConfirmCurrentExecutionAsync("发送图片", $"将通过当前微信发送 {files.Count} 张图片到当前会话。");
        if (confirm != true)
        {
            return;
        }

        await ExecuteMediaUploadAsync("正在上传图片...", async () =>
        {
            var urls = new List<string>();
            foreach (var file in files)
            {
                urls.Add(await SaveChatUploadAsync(file, "chat_pic", "pic", MaxChatImageSize));
            }

            // Android 15 上 SendMultiPictureTask 会走 SendImgProxyUI，可能因 MediaProvider 权限被拒但仍回报成功。
            // 这里统一逐张走 TalkToFriendTask 图片链，避免网页显示“下发成功”但微信实际没发。
            var sentCount = 0;
            foreach (var url in urls)
            {
                if (await Store.SendMessageAsync(url, type: 2))
                {
                    sentCount++;
                    await Task.Delay(350);
                }
            }

            var sent = sentCount == urls.Count;
            NoticeService.Notify(sent ? NotificationSeverity.Success : NotificationSeverity.Error,
                sent ? "图片已发送" : "图片发送失败",
                sent ? $"已逐张下发 {sentCount}/{urls.Count} 张图片。" : $"仅成功下发 {sentCount}/{urls.Count} 张，请查看服务端和安卓端日志。");
        });
    }

    private async Task UploadAndSendVideoAsync(InputFileChangeEventArgs args)
    {
        if (!EnsureCanSendMedia())
        {
            return;
        }

        var file = args.File;
        if (file == null)
        {
            return;
        }

        if (file.Size <= 0 || file.Size > MaxChatVideoSize || !IsContentType(file, "video/"))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "视频无效", "请选择 200MB 以内的视频文件。");
            return;
        }

        WarnIfUploadBaseUrlMayBeUnreachable();

        var confirm = await ConfirmCurrentExecutionAsync("发送视频", $"将通过当前微信发送视频文件：{file.Name}");
        if (confirm != true)
        {
            return;
        }

        await ExecuteMediaUploadAsync("正在上传视频...", async () =>
        {
            var url = await SaveChatUploadAsync(file, "chat_video", "video", MaxChatVideoSize, requireServed: true);
            var sent = await Store.SendMessageAsync(url, type: 4);
            NoticeService.Notify(sent ? NotificationSeverity.Success : NotificationSeverity.Error,
                sent ? "视频已发送" : "视频发送失败",
                sent ? "视频任务已下发。" : "请查看服务端和安卓端日志。");
        });
    }

    private async Task UploadAndSendFileAsync(InputFileChangeEventArgs args)
    {
        if (!EnsureCanSendMedia())
        {
            return;
        }

        var file = args.File;
        if (file == null)
        {
            return;
        }

        if (file.Size <= 0 || file.Size > MaxChatFileSize)
        {
            NoticeService.Notify(NotificationSeverity.Warning, "文件无效", "请选择 200MB 以内的文件。");
            return;
        }

        WarnIfUploadBaseUrlMayBeUnreachable();

        var confirm = await ConfirmCurrentExecutionAsync("发送文件", $"将通过当前微信发送文件：{file.Name}");
        if (confirm != true)
        {
            return;
        }

        await ExecuteMediaUploadAsync("正在上传文件...", async () =>
        {
            var url = await SaveChatUploadAsync(file, "chat_file", "files", MaxChatFileSize, requireServed: true);
            var payload = JsonSerializer.Serialize(new
            {
                name = file.Name,
                url
            });
            var sent = await Store.SendMessageAsync(payload, type: 8);
            NoticeService.Notify(sent ? NotificationSeverity.Success : NotificationSeverity.Error,
                sent ? "文件已发送" : "文件发送失败",
                sent ? "文件任务已下发。" : "请查看服务端和安卓端日志。");
        });
    }

    private async Task ExecuteMediaUploadAsync(string status, Func<Task> action)
    {
        _isUploadingMedia = true;
        _uploadStatus = status;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            NoticeService.Notify(NotificationSeverity.Error, "上传/发送失败", ex.Message);
        }
        finally
        {
            _uploadStatus = string.Empty;
            _isUploadingMedia = false;
        }
    }

    /// <summary>
    /// 保存聊天素材并生成安卓端可访问 URL。
    /// <para>沿用 AddFriend 页的本地上传方式，不触碰数据库持久化。保存后会先做一次静态文件 URL 自检，避免把 404 地址下发给安卓端。</para>
    /// </summary>
    private async Task<string> SaveChatUploadAsync(IBrowserFile file, string bizType, string subFolder, long maxSize, bool requireServed = false)
    {
        var storePath = Config["FileUploadSettings:StorePath"];
        if (string.IsNullOrWhiteSpace(storePath))
        {
            storePath = "wwwroot/uploads";
        }

        var uploadRoot = Path.IsPathRooted(storePath)
            ? storePath
            : Path.Combine(Directory.GetCurrentDirectory(), storePath);

        var ownerWxid = FirstNonEmpty(Store.SelectedDevice?.weChatId, Store.SelectedDevice?.wx?.wechatAccount?.wxid, "unknown");
        var subDir = Path.Combine("web", "chat", SanitizePathToken(ownerWxid), subFolder);
        var uploadDir = Path.Combine(uploadRoot, subDir);
        Directory.CreateDirectory(uploadDir);

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = InferExtension(file.ContentType, bizType);
        }

        var fileName = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDir, fileName);

        await using (var output = File.Create(filePath))
        await using (var input = file.OpenReadStream(maxSize))
        {
            await input.CopyToAsync(output);
        }

        var requestPrefix = Config["FileUploadSettings:RequestUrlPrefix"] ?? "uploads";
        requestPrefix = requestPrefix.Trim('/');
        var relativeUrl = $"{requestPrefix}/web/chat/{SanitizePathToken(ownerWxid)}/{subFolder}/{fileName}".Replace("\\", "/");
        var uploadUrl = BuildAbsoluteUploadUrl(relativeUrl);
        await EnsureUploadUrlCanBeServedAsync(uploadUrl, filePath, file.Size, requireServed);
        return uploadUrl;
    }

    /// <summary>
    /// 在下发安卓端之前确认上传文件已经能通过静态文件 URL 访问。
    /// <para>最新真机日志里图片 URL 返回 200，但同一目录下 .mkv 返回 404。这里把这类 StorePath/RequestUrlPrefix/静态文件 MIME 映射问题提前暴露在网页端。</para>
    /// </summary>
    private async Task EnsureUploadUrlCanBeServedAsync(string uploadUrl, string filePath, long expectedSize, bool requireServed)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"上传文件保存失败：本地文件不存在，Path={filePath}");
        }

        var fileInfo = new System.IO.FileInfo(filePath);
        if (fileInfo.Length <= 0)
        {
            throw new InvalidOperationException($"上传文件保存失败：本地文件为空，Path={filePath}");
        }

        if (expectedSize > 0 && fileInfo.Length != expectedSize)
        {
            NoticeService.Notify(
                NotificationSeverity.Warning,
                "上传文件大小不一致",
                $"浏览器声明 {expectedSize} 字节，服务端落盘 {fileInfo.Length} 字节，将继续尝试发送。");
        }

        var lastStatus = string.Empty;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (await ProbeUploadUrlAsync(uploadUrl, HttpMethod.Head, readBody: false))
                {
                    return;
                }

                lastStatus = _lastUploadProbeStatus;

                // 只要 HEAD 没拿到成功，就用 Range GET 再探一次。
                // 部分反代或静态文件中间件会对 HEAD 返回 405/404，但 GET 可正常服务；
                // 真正的 404 需要在 GET 后再判定，避免误拦截。
                if (await ProbeUploadUrlAsync(uploadUrl, HttpMethod.Get, readBody: true))
                {
                    return;
                }

                lastStatus = _lastUploadProbeStatus;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(250 * attempt);
        }

        if (string.Equals(lastStatus, "404", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"上传文件已保存，但静态文件 URL 返回 404：{uploadUrl}。请检查 SCRM.API 是否已重启到最新版本，以及 FileUploadSettings:StorePath 与 RequestUrlPrefix 是否和 Program.cs 的 UseStaticFiles 映射一致。");
        }

        if (!string.IsNullOrWhiteSpace(lastStatus))
        {
            if (requireServed)
            {
                throw new InvalidOperationException(
                    $"上传文件已保存，但静态文件 URL 自检未通过，已阻止下发到安卓端：{uploadUrl}，HTTP状态={lastStatus}。请检查 SCRM.API 静态文件映射、MIME 配置和 httpApiBaseUrl。");
            }

            NoticeService.Notify(
                NotificationSeverity.Warning,
                "上传地址自检未通过",
                $"URL={uploadUrl}，HTTP状态={lastStatus}。如果安卓端下载失败，请优先检查静态文件映射。");
            return;
        }

        if (lastException != null)
        {
            if (requireServed)
            {
                throw new InvalidOperationException(
                    $"上传文件已保存，但无法确认静态文件 URL 可访问，已阻止下发到安卓端：{uploadUrl}，原因：{lastException.Message}。请检查 SCRM.API 是否在 httpApiBaseUrl 上可访问。");
            }

            NoticeService.Notify(
                NotificationSeverity.Warning,
                "上传地址自检失败",
                $"无法从服务端探测 URL={uploadUrl}，原因：{lastException.Message}。将继续下发，若安卓端下载失败请检查 httpApiBaseUrl 网络可达性。");
        }
    }

    private string _lastUploadProbeStatus = string.Empty;

    /// <summary>
    /// 探测上传 URL 是否可访问。GET 探测只读取响应头，避免大视频被服务端重复读入内存。
    /// </summary>
    private async Task<bool> ProbeUploadUrlAsync(string uploadUrl, HttpMethod method, bool readBody)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(method, uploadUrl);
        if (readBody)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
        }

        using var response = await UploadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        _lastUploadProbeStatus = ((int)response.StatusCode).ToString();
        return response.IsSuccessStatusCode;
    }

    private void WarnIfUploadBaseUrlMayBeUnreachable()
    {
        if (!HasLikelyAndroidReachableBaseUrl(out var warning))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "上传地址可能不可达", warning);
        }
    }

    /// <summary>
    /// 检查生成的媒体 URL 是否大概率能被手机访问。
    /// <para>如果 httpApiBaseUrl/ApiSettings 仍是 localhost，安卓端下载会失败并造成“网页下发成功但微信没发”。</para>
    /// </summary>
    private bool HasLikelyAndroidReachableBaseUrl(out string warning)
    {
        warning = string.Empty;
        var baseUrl = string.IsNullOrWhiteSpace(_httpApiBaseUrl) ? NavManager.BaseUri : _httpApiBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            warning = "当前媒体基础地址无效，请检查系统配置 httpApiBaseUrl。";
            return false;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            warning = $"当前媒体基础地址为 {baseUrl}，手机通常无法访问本机 localhost，请改为电脑局域网 IP，例如 http://192.168.x.x:42718。";
            return false;
        }

        return true;
    }

    private bool EnsureCanSendMedia()
    {
        if (Store.SelectedDevice == null || (Store.SelectedConversation == null && Store.SelectedContact == null))
        {
            NoticeService.Notify(NotificationSeverity.Warning, "无法发送", "请先选择在线设备和会话。");
            return false;
        }

        return true;
    }

    private async Task LoadUploadBaseUrlAsync()
    {
        try
        {
            var apiBaseConfig = await SystemConfigService.GetConfigByKeyAsync("httpApiBaseUrl");
            _httpApiBaseUrl = apiBaseConfig?.value?.Trim() ?? string.Empty;
        }
        catch
        {
            _httpApiBaseUrl = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_httpApiBaseUrl))
        {
            _httpApiBaseUrl = Config["ApiSettings:BaseUrl"]?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_httpApiBaseUrl))
        {
            _httpApiBaseUrl = NavManager.BaseUri;
        }
    }

    private string BuildAbsoluteUploadUrl(string relativeUrl)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_httpApiBaseUrl) ? NavManager.BaseUri : _httpApiBaseUrl;
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        return new Uri(new Uri(baseUrl), relativeUrl.TrimStart('/')).ToString();
    }

    private static string SanitizePathToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "unknown";
        }

        foreach (var ch in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(ch, '_');
        }

        return input.Replace("..", "_").Replace("/", "_").Replace("\\", "_");
    }

    private static bool IsContentType(IBrowserFile file, string prefix)
    {
        return !string.IsNullOrWhiteSpace(file.ContentType)
            && file.ContentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string InferExtension(string? contentType, string bizType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/jpeg" => ".jpg",
            "video/mp4" => ".mp4",
            "video/quicktime" => ".mov",
            "video/x-matroska" => ".mkv",
            "video/webm" => ".webm",
            "video/x-m4v" => ".m4v",
            "video/3gpp" => ".3gp",
            "video/x-msvideo" => ".avi",
            "audio/mpeg" => ".mp3",
            "audio/wav" => ".wav",
            "audio/amr" => ".amr",
            _ when bizType.Contains("video", StringComparison.OrdinalIgnoreCase) => ".mp4",
            _ when bizType.Contains("pic", StringComparison.OrdinalIgnoreCase) => ".jpg",
            _ => ".bin"
        };
    }

    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(_msgContent) || Store.SelectedDevice == null || (Store.SelectedContact == null && Store.SelectedConversation == null)) return;

        _sending = true;
        try
        {
            var result = await Store.SendMessageAsync(_msgContent);
            if (result)
            {
                _msgContent = "";
            }
            else
            {
                NoticeService.Notify(NotificationSeverity.Error, "Failed to send", "消息发送失败");
            }
        }
        finally
        {
            _sending = false;
        }
    }

    private async Task HandleKeyPress(KeyboardEventArgs e)
    {
        if (e.CtrlKey && (e.Key == "Enter" || e.Key == "NumpadEnter"))
        {
            await Send();
        }
    }

    private async Task ScrollToBottom()
    {
        try
        {
            await JS.InvokeVoidAsync("scrmScrollToBottom", "im-chat-box");
        }
        catch { }
    }

    #endregion
}
