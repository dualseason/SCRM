using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SCRM.Services.Auth;
using SCRM.Services.Data;
using SCRM.Models.Entities;
using SCRM.Services;
using SCRM.Models.Configurations;
using SCRM.Core.Netty;
using SCRM.Services.Middleware;
using System.Text;
using Serilog;
using System.Globalization;
using SCRM.Shared.Core;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 初始化配置文件
        if (!SCRM.Shared.Core.AppConfigurationManager.InitSettings("server"))
        {
            Console.WriteLine("初始化配置文件失败，请检查配置文件路径");
            return;
        }

        // 初始化 Serilog（Debug & Console）
        SCRM.Shared.Core.Utility.logger = new LoggerConfiguration()
            .WriteTo.Debug(outputTemplate: "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // EF 错误日志（仅 Warning 及以上）
        SCRM.Shared.Core.Utility.efLogger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Debug(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning,
                          outputTemplate: "{Timestamp:HH:mm:ss.fff} 【EF错误】 {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning,
                              outputTemplate: "{Timestamp:HH:mm:ss.fff} 【EF错误】 {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // PostgreSQL 日志持久化
        var pgConnectionString = builder.Configuration.GetConnectionString("PostgresConnection");
        var columnWriters = new Dictionary<string, Serilog.Sinks.PostgreSQL.ColumnWriterBase>
        {
            {"Timestamp", new Serilog.Sinks.PostgreSQL.TimestampColumnWriter(NpgsqlTypes.NpgsqlDbType.TimestampTz)},
            {"Level", new Serilog.Sinks.PostgreSQL.LevelColumnWriter(true, NpgsqlTypes.NpgsqlDbType.Text)},
            {"Message", new Serilog.Sinks.PostgreSQL.RenderedMessageColumnWriter(NpgsqlTypes.NpgsqlDbType.Text)},
            {"Exception", new Serilog.Sinks.PostgreSQL.ExceptionColumnWriter(NpgsqlTypes.NpgsqlDbType.Text)}
        };

        SCRM.Shared.Core.Utility.loggerToDB = new LoggerConfiguration()
            .WriteTo.PostgreSQL(pgConnectionString,
                              tableName: "Logs",
                              needAutoCreateTable: true,
                              columnOptions: columnWriters)
            .CreateLogger();

        // 将 Serilog 注入 ASP.NET Core 主机
        Log.Logger = SCRM.Shared.Core.Utility.logger;

        builder.Host.UseSerilog();

        // Add services to the container.
        // Add EF Core PostgreSQL
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Add Connection Manager
        builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

        // Add Netty services
        builder.Services.AddSingleton<SCRM.Core.Netty.MessageRouter>();
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
        // Add Authorization services
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, SCRMAuthorizationHandler>();

        // Configure middleware options
        builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));
        builder.Services.Configure<HealthCheckOptions>(builder.Configuration.GetSection("HealthChecks"));

        builder.Services.AddAuthorization(options =>
        {
            // Add policy-based authorization
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("Admin", "SuperAdmin"));

            options.AddPolicy("CanManageUsers", policy =>
                policy.AddRequirements(new SCRMRequirement(permissions: new[] { "user.manage" })));

            options.AddPolicy("CanViewCustomers", policy =>
                policy.AddRequirements(new SCRMRequirement(permissions: new[] { "customer.view" })));

            options.AddPolicy("SalesOrManager", policy =>
                policy.AddRequirements(new SCRMRequirement(roles: new[] { "Sales", "Manager", "Admin" })));
        });

        // Add Bulk Operations service


        // Add CORS
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

        // Map controllers
        app.MapControllers();

        app.Run();
    }
}
