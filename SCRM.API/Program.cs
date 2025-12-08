using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using SCRM.Services.Data;
using SCRM.API.Models.Entities;
using SCRM.Services;
using SCRM.Models.Configurations;
using SCRM.Core.Netty;

using System.Text;
using Serilog;
using System.Globalization;
using SCRM.Shared.Core;
using SCRM.API.Hubs;
using Microsoft.AspNetCore.Identity;
using SCRM.SHARED.Models;

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
        builder.Services.AddMemoryCache();
        // Add EF Core Database
        if (builder.Environment.IsDevelopment())
        {
            // Use SQLite in development
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=scrm_dev.db"));
        }
        else
        {
            // Use PostgreSQL in production
            var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
            dataSourceBuilder.EnableDynamicJson();
            var dataSource = dataSourceBuilder.Build();
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(dataSource));
        }
        // Configure JWT settings
        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection("JwtSettings"));
            
        // Configure Netty settings
        builder.Services.Configure<NettySettings>(
            builder.Configuration.GetSection("NettySettings"));

        // Add JWT services
        builder.Services.AddScoped<JwtService>();

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
        });

        // Add Authorization services
        builder.Services.AddScoped<PermissionService>();

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
        builder.Services.AddSingleton<MessageRouter>();
        builder.Services.AddSingleton<NettyServer>();
        builder.Services.AddSingleton<NettyMessageService>();
        builder.Services.AddSingleton<ClientTaskService>();
        builder.Services.AddHostedService<NettyMessageService>(provider => provider.GetRequiredService<NettyMessageService>());        // Add CORS
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

        builder.Services.AddSignalR();
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
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

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            
            // Redirect root to Swagger UI
            app.MapGet("/", async context =>
            {
                context.Response.Redirect("/swagger");
                await Task.CompletedTask;
            });
        }

        // Enable CORS
        app.UseCors("AllowAll");

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

        // Ensure database is created and migrated
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var dbContext = services.GetRequiredService<ApplicationDbContext>();

                // Create database if it doesn't exist
                dbContext.Database.EnsureCreated();

                // Apply pending migrations
                dbContext.Database.Migrate();

                // Initialize Seed Data
                SCRM.API.Data.SeedData.InitializeAsync(services).Wait();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred initializing the database.");
            }
        }

        app.Run();
    }
}
