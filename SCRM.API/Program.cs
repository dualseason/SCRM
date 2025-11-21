using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SCRM.Authorization;
using SCRM.Data;
using SCRM.Models.Identity;
using SCRM.Services;
using SCRM.Configurations;
using SCRM.Hubs;
using SCRM.Netty;
using SCRM.Middleware;
using System.Text;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

// Add EF Core PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure RocketMQ settings
builder.Services.Configure<RocketMQSettings>(
    builder.Configuration.GetSection("RocketMQ"));

// Add RocketMQ services (using Mock versions for testing)
builder.Services.AddSingleton<IRocketMQProducerService, MockRocketMQProducerService>();
builder.Services.AddSingleton<IRocketMQConsumerService, MockRocketMQConsumerService>();

// Add SignalR services
builder.Services.AddSignalR(config =>
{
    config.EnableDetailedErrors = true;
    config.KeepAliveInterval = TimeSpan.FromSeconds(15);
    config.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    config.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// Add Connection Manager
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// Add SignalR Message Service
builder.Services.AddSingleton<ISignalRMessageService, SignalRMessageService>();

// Add RocketMQ-SignalR Integration as Background Service
builder.Services.AddHostedService<RocketMQSignalRIntegration>();

// Add Netty services
builder.Services.AddSingleton<INettyServer, NettyServer>();
builder.Services.AddSingleton<INettyMessageService, NettyMessageService>();
builder.Services.AddHostedService<NettyMessageService>();

// Configure Redis settings
builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection("Redis"));

// Add Redis services
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetSection("Redis:ConnectionString").Value;
    options.InstanceName = builder.Configuration.GetSection("Redis:InstanceName").Value;
});

// Add Memory Cache service for PermissionService
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IRedisCacheService, SimpleRedisCacheService>();

// Configure JWT settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

// Add JWT services
builder.Services.AddScoped<IJwtService, JwtService>();

// Configure Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
var key = Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? "DefaultSecretKey123456789");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings?.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

// Add Authorization services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionsAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RolesAuthorizationHandler>();

// Configure middleware options
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.Configure<HealthCheckOptions>(builder.Configuration.GetSection("HealthChecks"));

builder.Services.AddAuthorization(options =>
{
    // Add policy-based authorization
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("CanManageUsers", policy =>
        policy.AddRequirements(new PermissionRequirement("user.manage")));

    options.AddPolicy("CanViewCustomers", policy =>
        policy.AddRequirements(new PermissionRequirement("customer.view")));

    options.AddPolicy("SalesOrManager", policy =>
        policy.AddRequirements(new RolesRequirement("Sales", "Manager", "Admin")));
});

// Add Bulk Operations service
builder.Services.AddScoped<IBulkOperationService, BulkOperationService>();
builder.Services.AddScoped<DatabaseInitializationService>();
builder.Services.AddScoped<PermissionInitializationService>();

// Add CORS for SignalR (required for Android app)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.SetIsOriginAllowed(_ => true)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS
app.UseCors("AllowAll");

// Add Health Check middleware
app.UseMiddleware<HealthCheckMiddleware>();

// Add Rate Limiting middleware
app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();

// Add Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR Hub
app.MapHub<SCRMHub>("/scrmhub");

// Map controllers
app.MapControllers();

      app.Run();
    }
}
