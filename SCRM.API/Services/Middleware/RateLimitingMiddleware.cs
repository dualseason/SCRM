using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SCRM.Services.Middleware;

public class RateLimitingMiddleware
{
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

    private readonly RequestDelegate _next;
        private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitCounter> _clients;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitOptions> options)
    {
        _next = next;        _options = options.Value;
        _clients = new ConcurrentDictionary<string, RateLimitCounter>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableRateLimit)
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var endpoint = GetEndpointKey(context);
        var key = $"{clientId}:{endpoint}";

        if (!_clients.TryGetValue(key, out var counter))
        {
            counter = new RateLimitCounter();
            _clients[key] = counter;
        }

        counter.Requests++;
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-_options.DefaultWindowMinutes);

        if (counter.LastRequestTime < windowStart)
        {
            counter.Requests = 1;
            counter.LastRequestTime = now;
        }

        var limit = GetRateLimit(context);

        if (counter.Requests > limit)
        {
            _logger.Warning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint}. Requests: {Requests}, Limit: {Limit}",
                clientId, endpoint, counter.Requests, limit);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = _options.DefaultWindowMinutes.ToString();

            var response = new
            {
                StatusCode = 429,
                Message = string.Format(_options.QuotaExceededResponse.Message, limit, _options.DefaultWindowMinutes),
                RequestCount = counter.Requests,
                Limit = limit,
                WindowMinutes = _options.DefaultWindowMinutes
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        context.Response.Headers["X-Rate-Limit-Limit"] = limit.ToString();
        context.Response.Headers["X-Rate-Limit-Remaining"] = Math.Max(0, limit - counter.Requests).ToString();
        context.Response.Headers["X-Rate-Limit-Reset"] = counter.LastRequestTime.AddMinutes(_options.DefaultWindowMinutes).ToString("yyyy-MM-ddTHH:mm:ssZ");

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Priority: API Key > User ID > IP Address
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst("nameid")?.Value;
            if (!string.IsNullOrEmpty(userIdClaim))
                return $"user:{userIdClaim}";
        }

        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
            return $"api:{apiKey}";

        var ipAddress = GetClientIpAddress(context);
        return $"ip:{ipAddress}";
    }

    private string GetEndpointKey(HttpContext context)
    {
        if (_options.EnableEndpointRateLimit)
        {
            return $"{context.Request.Method}:{context.Request.Path}";
        }
        return "global";
    }

    private int GetRateLimit(HttpContext context)
    {
        // Custom rate limits based on endpoint or user role
        if (context.User.IsInRole("SuperAdmin"))
            return _options.DefaultLimit * 5;

        if (context.User.IsInRole("Admin"))
            return _options.DefaultLimit * 3;

        // Specific endpoint limits
        var path = context.Request.Path.Value?.ToLower();
        if (path?.Contains("/auth/login") == true)
            return _options.DefaultLimit / 2; // Stricter limit for login

        if (path?.Contains("/api/test") == true)
            return _options.DefaultLimit / 5; // Very strict for test endpoints

        return _options.DefaultLimit;
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ipAddress))
        {
            return ipAddress.Split(',')[0].Trim();
        }

        ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ipAddress))
        {
            return ipAddress;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public class RateLimitOptions
{
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

    public bool EnableRateLimit { get; set; } = true;
    public int DefaultLimit { get; set; } = 100;
    public int DefaultWindowMinutes { get; set; } = 1;
    public bool EnableEndpointRateLimit { get; set; } = true;
    public QuotaExceededResponse QuotaExceededResponse { get; set; } = new();
}

public class QuotaExceededResponse
{
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

    public int StatusCode { get; set; } = 429;
    public string Message { get; set; } = "API calls quota exceeded! Please try again later.";
}

public class RateLimitCounter
{
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

    public int Requests { get; set; }
    public DateTime LastRequestTime { get; set; } = DateTime.UtcNow;
}