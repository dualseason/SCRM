using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using SCRM.Services.Data;
using SCRM.API.Models.DTOs;
using SCRM.Services.Events;
using SCRM.Services;
using SCRM.Models.Configurations;
using SCRM.API.Services.Core;
using SCRM.Services.Netty;
using SCRM.API.Services.Core;
using SCRM.API.Services.Events;

using System.Text;
using Serilog;
using System.Globalization;
using SCRM.Shared.Core;
using SCRM.API.Hubs;
using Microsoft.AspNetCore.Identity;
using SCRM.SHARED.Models;
using SCRM.Shared.Interfaces;
using SCRM.API.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using SCRM.UI.Services;

public partial class Program
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

        // 初始化 Serilog (统一配置)
        // 读取配置
        var pgConnectionString = builder.Configuration.GetConnectionString("PostgresConnection");
        
        // PostgreSQL 列映射配置
        var columnWriters = new Dictionary<string, Serilog.Sinks.PostgreSQL.ColumnWriterBase>
        {
            {"Timestamp", new Serilog.Sinks.PostgreSQL.TimestampColumnWriter(NpgsqlTypes.NpgsqlDbType.TimestampTz)},
            {"Level", new Serilog.Sinks.PostgreSQL.LevelColumnWriter(true, NpgsqlTypes.NpgsqlDbType.Text)},
            {"Message", new Serilog.Sinks.PostgreSQL.RenderedMessageColumnWriter(NpgsqlTypes.NpgsqlDbType.Text)},
            {"Exception", new Serilog.Sinks.PostgreSQL.ExceptionColumnWriter(NpgsqlTypes.NpgsqlDbType.Text)}
        };

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration) // 读取 appsettings.json (包括 LogLevel Overrides)
            .Enrich.FromLogContext()
            .WriteTo.Debug(
                outputTemplate: "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose // 允许 Debug 窗口显示所有级别的日志（受限于 appsettings.json）
            )
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information
            );

        // 如果配置了 PostgreSQL 连接字符串，则添加 PostgreSQL Sink
        if (!string.IsNullOrEmpty(pgConnectionString))
        {
             loggerConfig.WriteTo.PostgreSQL(
                connectionString: pgConnectionString,
                tableName: "Logs",
                needAutoCreateTable: true,
                columnOptions: columnWriters,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning // 仅记录警告及以上到数据库，避免撑爆
            );
        }

        Log.Logger = loggerConfig.CreateLogger();

        // 绑定到 Host
        builder.Host.UseSerilog();



        // Add services to the container.
        builder.Services.AddMemoryCache();
        // Add EF Core PostgreSQL
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(dataSource));
        // Configure JWT settings
        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection("JwtSettings"));
            
        // Configure Netty settings (Added Back)
        builder.Services.Configure<NettySettings>(
            builder.Configuration.GetSection("NettySettings"));


        // Add JWT services
        // Add JWT services
        // Services consolidated into AuthService
        // builder.Services.AddScoped<JwtService>(); -- Removed
        // builder.Services.AddScoped<PermissionService>(); -- Removed
        builder.Services.AddScoped<AuthService>();

        // Configure Identity
        builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
        })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

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

            // Configure SignalR token reading from query string
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    
                    // Console.WriteLine($"[Auth] Processing Request: {path}, TokenQuery: {accessToken}");

                    if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/fileUpload")))
                    {
                        context.Token = accessToken;
                        // Console.WriteLine("[Auth] Token extracted from QueryString for SignalR.");
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                   // Console.WriteLine($"[Auth] Authentication Failed: {context.Exception.Message}");
                   return Task.CompletedTask;
                }
            };
        });

        // Add Authorization services
        // PermissionService removed - consolidated into AuthService

        builder.Services.AddAuthorization(options =>
        {
            // Add simple role-based authorization
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("Admin", "SuperAdmin"));

            options.AddPolicy("RequireManagerRole", policy =>
                policy.RequireRole("Manager", "Admin", "SuperAdmin"));
        });

        // Add Bulk Operations service

        // Netty & C&C Services
        builder.Services.AddSingleton<ConnectionManager>();
        builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
        builder.Services.AddSingleton<MessageRouter>();
        builder.Services.AddSingleton<NettyServer>();
        builder.Services.AddSingleton<NettyMessageService>();
        builder.Services.AddSingleton<SCRM.API.Services.Core.ClientTaskService>();
        builder.Services.AddScoped<ISystemLogService, SystemLogService>();

        // Blazor Server Services
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddScoped<SCRM.UI.Services.CrmStore>(); // Required for Blazor Server Injection
        
        // Radzen Services
        builder.Services.AddScoped<Radzen.DialogService>();
        builder.Services.AddScoped<Radzen.NotificationService>();
        builder.Services.AddScoped<Radzen.TooltipService>();
        builder.Services.AddScoped<Radzen.ContextMenuService>();
        
        // System Config Service (Database Based - KISS)
        builder.Services.AddScoped<ISystemConfigService, ServerSystemConfigService>();
        
        // Device Command Service (Direct SignalR Hub Access)
        builder.Services.AddScoped<ServerDeviceCommandService>();
        
        // HttpClient for Local API Calls (Blazor Server Monolith Pattern)
        builder.Services.AddHttpClient();
        
        // Storage Service
        builder.Services.AddBlazoredLocalStorage();
        
        // Custom Auth State Provider
        builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
        builder.Services.AddAuthorizationCore();

        // SCRM Architecture v5 Services (Skeleton)
        builder.Services.AddScoped<ICrmService, CrmService>();
        // Event Aggregator must be Singleton to share state
        builder.Services.AddSingleton<CrmEventAggregator>();
        builder.Services.AddSingleton<ICrmEvents>(sp => sp.GetRequiredService<CrmEventAggregator>());
        builder.Services.AddSingleton<ICrmEventPublisher>(sp => sp.GetRequiredService<CrmEventAggregator>());


        builder.Services.AddHostedService<NettyMessageService>(provider => provider.GetRequiredService<NettyMessageService>());        
        builder.Services.AddHostedService<EventForwardingService>(); // Forward events to SignalR
        
        // Netty Handlers (Scoped)
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.AuthMessageHandler>();
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.TaskMessageHandler>();
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.SystemMessageHandler>();
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.ChatMessageHandler>();
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.ContactMessageHandler>();
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.GroupMessageHandler>();
        builder.Services.AddScoped<SCRM.API.Services.Netty.Handlers.MomentsMessageHandler>();
        
        builder.Services.AddHostedService<SCRM.Services.Automation.AutomationService>(); // C&C Automation (Auto-Reply, etc.)
        builder.Services.AddHostedService<SCRM.API.Services.Maintenance.IndexCleanupService>(); // Auto-fix zombie indexes
        builder.Services.AddHostedService<SCRM.API.Services.Maintenance.MessageCleanupService>(); // Auto-delete old messages
        builder.Services.AddHostedService<SCRM.API.Services.Data.CacheWarmupService>(); // KISS: Cache Warmup on Startup
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

        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        /*
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "SCRM.API - 微信客服系统",
                Version = "v1",
                Description = "微信客服系统 API 文档",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "SCRM Team",
                    Url = new Uri("https://github.com/yourusername/SCRM.SOLUTION")
                },
                License = new Microsoft.OpenApi.Models.OpenApiLicense
                {
                    Name = "MIT License"
                }
            });

            // Add JWT Bearer token support in Swagger
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "请输入 Bearer token",
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            });

            // Note: EnableAnnotations requires Swashbuckle.AspNetCore.Annotations package
            // c.EnableAnnotations();

            // Include XML comments for better documentation
            var xmlFile = Path.Combine(AppContext.BaseDirectory, "SCRM.API.xml");
            if (File.Exists(xmlFile))
                c.IncludeXmlComments(xmlFile);
        });
        */

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            // app.UseSwagger();
            // app.UseSwaggerUI();
            
            // Redirect root to Swagger UI -- DISABLED
            // Redirect root to Swagger UI -- DISABLED
            // app.MapGet("/", async context =>
            // {
            //     await context.Response.WriteAsync("SCRM API Running");
            // });
        }

        // Enable CORS
        app.UseCors("AllowAll");

        // Enable Static Files for File Uploads
        var uploadSettings = app.Configuration.GetSection("FileUploadSettings");
        var storePath = uploadSettings["StorePath"];
        var requestPrefix = uploadSettings["RequestUrlPrefix"] ?? "uploads";

        // Fix: ALWAYS serve standard static files (wwwroot) like site.css, bootstrap.css, blazor.server.js
        // If we don't do this, enabling file uploads effectively disables the entire website's styling and scripts.
        app.UseStaticFiles();

        if (!string.IsNullOrEmpty(storePath) && Path.IsPathRooted(storePath))
        {
            // Absolute Path (e.g. C:/Uploads)
            if (!Directory.Exists(storePath)) Directory.CreateDirectory(storePath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storePath),
                RequestPath = "/" + requestPrefix.Trim('/')
            });
        }
        // Removed 'else' block because wwwroot must allow be served regardless of upload config

        app.UseRouting();

        // Add Health Check middleware
        app.UseMiddleware<HealthCheckMiddleware>();

        // Add Rate Limiting middleware
        // Add Rate Limiting middleware
        // app.UseRateLimiter(); // TODO: Configure built-in rate limiter if needed

        // app.UseHttpsRedirection();

        // Add Authentication & Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map controllers
        app.MapControllers();
        
        // Map SignalR Hubs
        app.MapHub<ClientHub>("/hubs/client");
        
        // Map Blazor Hub (Architecture v5)
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        // Initialize Seed Data and ensure DB exists + migrated
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var dbContext = services.GetRequiredService<ApplicationDbContext>();
                
                // 强制优先建立表结构和执行所有堆积的 Migration
                dbContext.Database.Migrate();

                SCRM.API.Data.SeedData.InitializeAsync(services).Wait();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred seeding the DB or during migration.");
            }
        }

        app.Run();
    }
}
