using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SCRM.API.Services.Security;

namespace SCRM.Tests;

/// <summary>
/// 受控媒体访问短期 token 防回归测试。
/// <para>后续 UI 逐步替换永久 /uploads URL 前，先保证后端 token、代理读取和权限入口不被破坏。</para>
/// </summary>
public sealed class MediaAccessTokenSecurityTests
{
    [Fact]
    public void MediaAccessTokenService_ShouldNormalizeUploadPathsAndInferOwnerTargets()
    {
        using var fixture = new MediaAccessTokenServiceFixture();
        var service = fixture.CreateService();

        Assert.True(service.TryNormalizeUploadRelativePath(
            "https://scrm.local/uploads/wx/wxid_owner/pic/a.jpg?version=1#top",
            out var wxRelativePath,
            out var wxError));
        Assert.Equal(string.Empty, wxError);
        Assert.Equal("wx/wxid_owner/pic/a.jpg", wxRelativePath);

        Assert.True(service.TryNormalizeUploadRelativePath(
            "/uploads/devices/device-a/screenshot/s1.png",
            out var deviceRelativePath,
            out var deviceError));
        Assert.Equal(string.Empty, deviceError);
        Assert.Equal("devices/device-a/screenshot/s1.png", deviceRelativePath);

        Assert.False(service.TryNormalizeUploadRelativePath("../secret.txt", out _, out var outsideError));
        Assert.Contains("uploads/", outsideError);

        Assert.False(service.TryNormalizeUploadRelativePath("uploads/../secret.txt", out _, out var traversalError));
        Assert.Contains("不合法", traversalError);

        var wxTarget = MediaAccessTokenService.InferTarget(wxRelativePath);
        Assert.Equal(MediaAccessTargetKind.WechatAccount, wxTarget.Kind);
        Assert.Equal("wxid_owner", wxTarget.Value);

        var deviceTarget = MediaAccessTokenService.InferTarget(deviceRelativePath);
        Assert.Equal(MediaAccessTargetKind.Device, deviceTarget.Kind);
        Assert.Equal("device-a", deviceTarget.Value);
    }

    [Fact]
    public void MediaAccessTokenService_ShouldRoundTripTokenAndResolveInsideUploadRootOnly()
    {
        using var fixture = new MediaAccessTokenServiceFixture();
        var service = fixture.CreateService();
        var relativePath = Path.Combine("wx", "wxid_owner", "voice", "v1.amr").Replace('\\', '/');
        var physicalPath = Path.Combine(fixture.UploadRoot, "wx", "wxid_owner", "voice", "v1.amr");
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.WriteAllText(physicalPath, "voice-bytes");

        var issued = service.CreateToken(relativePath, MediaAccessTokenService.CallRecordingScope, "user-a", TimeSpan.FromMinutes(5));

        Assert.Equal(relativePath, issued.RelativePath);
        Assert.Equal(MediaAccessTokenService.CallRecordingScope, issued.Scope);
        Assert.True(service.TryUnprotect(issued.Token, out var payload, out var expiresAt, out var error));
        Assert.Equal(string.Empty, error);
        Assert.Equal(relativePath, payload.RelativePath);
        Assert.Equal("user-a", payload.UserId);
        Assert.True(expiresAt > DateTimeOffset.UtcNow);

        Assert.True(service.TryResolvePhysicalPath(payload.RelativePath, out var resolvedPath, out var resolveError));
        Assert.Equal(string.Empty, resolveError);
        Assert.Equal(Path.GetFullPath(physicalPath), resolvedPath);

        Assert.False(service.TryResolvePhysicalPath("../outside.txt", out _, out var outsideError));
        Assert.Contains("不合法", outsideError);
    }

    [Fact]
    public void MediaAccessTokenService_ShouldUseTimeLimitedProtectorWithoutStoringAbsoluteUrl()
    {
        var source = ReadSource("SCRM.API", "Services", "Security", "MediaAccessTokenService.cs");

        Assert.Contains("ITimeLimitedDataProtector", source);
        Assert.Contains("CreateProtector(\"SCRM.MediaAccessToken.v1\")", source);
        Assert.Contains("ToTimeLimitedDataProtector()", source);
        Assert.Contains("_protector.Protect(json, lifetime)", source);
        Assert.Contains("_protector.Unprotect(token, out expiresAt)", source);
        Assert.Contains("TryNormalizeUploadRelativePath", source);
        Assert.Contains("TryResolvePhysicalPath", source);
        Assert.Contains("Path.GetFullPath", source);
        Assert.Contains("MediaAccessTokenPayload", source);
        Assert.Contains("public string RelativePath { get; set; }", source);
        Assert.Contains("public string Scope { get; set; }", source);
        Assert.Contains("public string UserId { get; set; }", source);
        Assert.DoesNotContain("public string PhysicalPath { get; set; }", source);
        Assert.DoesNotContain("public string OriginalUrl { get; set; }", source);
    }

    [Fact]
    public void MediaAccessController_ShouldExposeAuthorizedIssueEndpointsAndAnonymousOpen()
    {
        var source = ReadSource("SCRM.API", "Controllers", "MediaAccessController.cs");

        Assert.Contains("[Route(\"api/media-access\")]", source);
        Assert.Contains("MediaAccessTokenService tokenService", source);
        Assert.Contains("SensitiveMaskingService sensitiveMaskingService", source);
        Assert.Contains("AccountAccessGuard accountAccessGuard", source);

        var createToken = ExtractMethod(source, "CreateToken(");
        Assert.Contains("[Authorize]", createToken);
        Assert.Contains("[HttpPost(\"token\")]", createToken);
        Assert.Contains("TryNormalizeUploadRelativePath(request.Url", createToken);
        Assert.Contains("NormalizeScope(request.Scope)", createToken);
        Assert.Contains("CanIssueTokenForRelativePathAsync", createToken);

        var createCallRecordingToken = ExtractMethod(source, "CreateCallRecordingToken(");
        Assert.Contains("[Authorize]", createCallRecordingToken);
        Assert.Contains("[HttpPost(\"call-recordings/{id:int}/token\")]", createCallRecordingToken);
        Assert.Contains("_db.CallLogRecords", createCallRecordingToken);
        Assert.Contains("CanAccessAccountAsync(User, record.ownerWxid)", createCallRecordingToken);
        Assert.Contains("profile.CanViewCallRecordUrl", createCallRecordingToken);
        Assert.Contains("TryNormalizeUploadRelativePath(record.recordUrl", createCallRecordingToken);

        var open = ExtractMethod(source, "Open(");
        Assert.Contains("[AllowAnonymous]", open);
        Assert.Contains("[HttpGet(\"open\")]", open);
        Assert.Contains("TryUnprotect(token", open);
        Assert.Contains("TryResolvePhysicalPath(payload.RelativePath", open);
        Assert.Contains("PhysicalFile(physicalPath, contentType, enableRangeProcessing: true)", open);
        Assert.Contains("X-Media-Token-Expires", open);

        var canIssue = ExtractMethod(source, "CanIssueTokenForRelativePathAsync(");
        Assert.Contains("BuildProfileAsync(User)", canIssue);
        Assert.Contains("profile.CanViewCallRecordUrl", canIssue);
        Assert.Contains("!profile.CanViewMessageRaw", canIssue);
        Assert.Contains("CanAccessDeviceAsync(User, deviceUuid)", canIssue);
        Assert.Contains("CanAccessAccountAsync(User, accountId)", canIssue);
        Assert.Contains("MediaAccessTokenService.InferTarget(relativePath)", canIssue);
        Assert.Contains("AccountAccessGuard.IsAdmin(User)", canIssue);
    }

    [Fact]
    public void Program_ShouldRegisterMediaAccessTokenService()
    {
        var source = ReadSource("SCRM.API", "Program.cs");

        Assert.Contains("AddScoped<SCRM.API.Services.Security.MediaAccessTokenService>()", source);
    }

    private static string ReadSource(params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { FindSolutionRoot() }.Concat(parts).ToArray()));
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var searchFrom = 0;
        while (true)
        {
            var methodNameIndex = source.IndexOf(methodName, searchFrom, StringComparison.Ordinal);
            Assert.True(methodNameIndex >= 0, $"未找到方法：{methodName}");

            var lineStart = source.LastIndexOf('\n', methodNameIndex);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var declarationPrefix = source[lineStart..methodNameIndex];
            if (!declarationPrefix.Contains("public ", StringComparison.Ordinal)
                && !declarationPrefix.Contains("private ", StringComparison.Ordinal))
            {
                searchFrom = methodNameIndex + methodName.Length;
                continue;
            }

            var start = lineStart;
            while (start > 0)
            {
                var previousLineEnd = start - 1;
                var previousLineStart = source.LastIndexOf('\n', Math.Max(0, previousLineEnd - 1));
                previousLineStart = previousLineStart < 0 ? 0 : previousLineStart + 1;
                var previousLine = source[previousLineStart..previousLineEnd].Trim();
                if (previousLine.StartsWith("[", StringComparison.Ordinal) || previousLine.Length == 0)
                {
                    start = previousLineStart;
                    continue;
                }

                break;
            }

            var braceStart = source.IndexOf('{', methodNameIndex);
            Assert.True(braceStart >= 0, $"未找到方法体：{methodName}");

            var header = source[start..braceStart];
            if (!header.Contains(methodName, StringComparison.Ordinal))
            {
                searchFrom = methodNameIndex + methodName.Length;
                continue;
            }

            var depth = 0;
            for (var index = braceStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source[start..(index + 1)];
                    }
                }
            }

            throw new InvalidOperationException($"方法体未闭合：{methodName}");
        }
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SCRM.SOLUTION.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private sealed class MediaAccessTokenServiceFixture : IDisposable
    {
        private readonly string _keyRingPath;

        public MediaAccessTokenServiceFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "scrm-media-token-tests", Guid.NewGuid().ToString("N"));
            UploadRoot = Path.Combine(Root, "uploads");
            _keyRingPath = Path.Combine(Root, "keys");
            Directory.CreateDirectory(UploadRoot);
            Directory.CreateDirectory(_keyRingPath);
        }

        public string Root { get; }

        public string UploadRoot { get; }

        public MediaAccessTokenService CreateService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileUploadSettings:RequestUrlPrefix"] = "uploads",
                    ["FileUploadSettings:StorePath"] = UploadRoot
                })
                .Build();
            var environment = new TestWebHostEnvironment(Root);
            var dataProtectionProvider = DataProtectionProvider.Create(new DirectoryInfo(_keyRingPath));

            return new MediaAccessTokenService(
                dataProtectionProvider,
                configuration,
                environment,
                NullLogger<MediaAccessTokenService>.Instance);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = Path.Combine(root, "wwwroot");
            ContentRootFileProvider = new PhysicalFileProvider(root);
            WebRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "SCRM.Tests";

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
