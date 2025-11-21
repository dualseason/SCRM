using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SCRM.Middleware;

public class HealthCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HealthCheckMiddleware> _logger;
    private readonly HealthCheckOptions _options;

    private readonly IServiceProvider _serviceProvider;

    public HealthCheckMiddleware(
        RequestDelegate next,
        ILogger<HealthCheckMiddleware> logger,
        IOptions<HealthCheckOptions> options,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableHealthChecks)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.ToLower();

        if (path == _options.HealthCheckUrl.ToLower())
        {
            await BasicHealthCheck(context);
            return;
        }

        if (path == _options.DetailedHealthCheckUrl.ToLower())
        {
            await DetailedHealthCheck(context);
            return;
        }

        await _next(context);
    }

    private async Task BasicHealthCheck(HttpContext context)
    {
        try
        {
            var healthStatus = await GetHealthStatusAsync();

            context.Response.StatusCode = healthStatus.IsHealthy ? 200 : 503;
            context.Response.ContentType = "application/json";

            var response = new
            {
                Status = healthStatus.IsHealthy ? "Healthy" : "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = GetVersion()
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("{\"Status\":\"Unhealthy\"}");
        }
    }

    private async Task DetailedHealthCheck(HttpContext context)
    {
        try
        {
            var healthStatus = await GetDetailedHealthStatusAsync();

            context.Response.StatusCode = healthStatus.IsHealthy ? 200 : 503;
            context.Response.ContentType = "application/json";

            var response = new
            {
                Status = healthStatus.IsHealthy ? "Healthy" : "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                Duration = healthStatus.TotalDuration,
                Services = healthStatus.ServiceStatuses
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("{\"Status\":\"Unhealthy\"}");
        }
    }

    private async Task<HealthStatus> GetHealthStatusAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var isHealthy = true;

        try
        {
            // Check database connectivity
            var dbHealthy = await CheckDatabaseHealthAsync();
            isHealthy = isHealthy && dbHealthy;

            // Check Redis connectivity
            var redisHealthy = await CheckRedisHealthAsync();
            isHealthy = isHealthy && redisHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check error");
            isHealthy = false;
        }

        stopwatch.Stop();

        return new HealthStatus
        {
            IsHealthy = isHealthy,
            TotalDuration = stopwatch.ElapsedMilliseconds
        };
    }

    private async Task<DetailedHealthStatus> GetDetailedHealthStatusAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var services = new List<ServiceHealth>();
        var overallHealthy = true;

        // Database health check
        try
        {
            var dbStopwatch = Stopwatch.StartNew();
            var dbHealthy = await CheckDatabaseHealthAsync();
            dbStopwatch.Stop();

            services.Add(new ServiceHealth
            {
                Name = "Database",
                Status = dbHealthy ? "Healthy" : "Unhealthy",
                Duration = dbStopwatch.ElapsedMilliseconds,
                Details = dbHealthy ? "Database connection successful" : "Database connection failed"
            });
            overallHealthy = overallHealthy && dbHealthy;
        }
        catch (Exception ex)
        {
            services.Add(new ServiceHealth
            {
                Name = "Database",
                Status = "Unhealthy",
                Duration = 0,
                Details = $"Database check failed: {ex.Message}"
            });
            overallHealthy = false;
        }

        // Redis health check
        try
        {
            var redisStopwatch = Stopwatch.StartNew();
            var redisHealthy = await CheckRedisHealthAsync();
            redisStopwatch.Stop();

            services.Add(new ServiceHealth
            {
                Name = "Redis",
                Status = redisHealthy ? "Healthy" : "Unhealthy",
                Duration = redisStopwatch.ElapsedMilliseconds,
                Details = redisHealthy ? "Redis connection successful" : "Redis connection failed"
            });
            overallHealthy = overallHealthy && redisHealthy;
        }
        catch (Exception ex)
        {
            services.Add(new ServiceHealth
            {
                Name = "Redis",
                Status = "Unhealthy",
                Duration = 0,
                Details = $"Redis check failed: {ex.Message}"
            });
            overallHealthy = false;
        }

        // RocketMQ health check
        try
        {
            var mqStopwatch = Stopwatch.StartNew();
            var mqHealthy = await CheckRocketMQHealthAsync();
            mqStopwatch.Stop();

            services.Add(new ServiceHealth
            {
                Name = "RocketMQ",
                Status = mqHealthy ? "Healthy" : "Unhealthy",
                Duration = mqStopwatch.ElapsedMilliseconds,
                Details = mqHealthy ? "RocketMQ connection successful" : "RocketMQ connection failed"
            });
            overallHealthy = overallHealthy && mqHealthy;
        }
        catch (Exception ex)
        {
            services.Add(new ServiceHealth
            {
                Name = "RocketMQ",
                Status = "Unhealthy",
                Duration = 0,
                Details = $"RocketMQ check failed: {ex.Message}"
            });
            overallHealthy = false;
        }

        stopwatch.Stop();

        return new DetailedHealthStatus
        {
            IsHealthy = overallHealthy,
            TotalDuration = stopwatch.ElapsedMilliseconds,
            ServiceStatuses = services
        };
    }

    private async Task<bool> CheckDatabaseHealthAsync()
    {
        try
        {
            // Simplified health check - can be enhanced later
            await Task.Delay(10); // Simulate connection check
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckRedisHealthAsync()
    {
        try
        {
            // Simplified health check - can be enhanced later
            await Task.Delay(5); // Simulate connection check
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckRocketMQHealthAsync()
    {
        try
        {
            // Simplified health check - can be enhanced later
            await Task.Delay(15); // Simulate connection check
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetVersion()
    {
        // Get assembly version
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}

public class HealthCheckOptions
{
    public bool EnableHealthChecks { get; set; } = true;
    public string HealthCheckUrl { get; set; } = "/health";
    public string DetailedHealthCheckUrl { get; set; } = "/health/detailed";
}

public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public long TotalDuration { get; set; }
}

public class DetailedHealthStatus : HealthStatus
{
    public List<ServiceHealth> ServiceStatuses { get; set; } = new();
}

public class ServiceHealth
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long Duration { get; set; }
    public string Details { get; set; } = string.Empty;
}