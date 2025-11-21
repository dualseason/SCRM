using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SCRM.Data;
using SCRM.Models.Identity;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace SCRM.TEST.Integration
{
    /// <summary>
    /// 简化的认证控制器集成测试
    /// </summary>
    public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AuthControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // 移除现有的数据库上下文
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // 添加内存数据库
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestAuthDb");
                        options.EnableSensitiveDataLogging();
                    });

                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<ApplicationDbContext>();

                    // 清理数据库
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();

                    // 添加测试数据
                    SeedTestData(db);
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOk()
        {
            // Arrange
            var loginRequest = new
            {
                username = "testuser",
                password = "TestPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<object>();
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new
            {
                username = "testuser",
                password = "wrongpassword"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_NonExistentUser_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new
            {
                username = "nonexistentuser",
                password = "password"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_EmptyUsername_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new
            {
                username = "",
                password = "password"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_EmptyPassword_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new
            {
                username = "testuser",
                password = ""
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetProfile_WithoutToken_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/api/auth/profile");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Logout_WithoutToken_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.PostAsync("/api/auth/logout", null);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static void SeedTestData(ApplicationDbContext context)
        {
            // 添加测试角色
            var userRole = new Role
            {
                Name = "User",
                Description = "Regular User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Roles.Add(userRole);

            // 添加测试用户
            var testUser = new User
            {
                UserName = "testuser",
                Email = "test@test.com",
                FirstName = "Test",
                LastName = "User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            context.IdentityUsers.Add(testUser);

            // 添加用户角色关联
            context.UserRoles.Add(new UserRole
            {
                UserId = testUser.Id,
                RoleId = userRole.Id,
                CreatedAt = DateTime.UtcNow
            });

            // 添加非活跃用户
            var inactiveUser = new User
            {
                UserName = "inactiveuser",
                Email = "inactive@test.com",
                FirstName = "Inactive",
                LastName = "User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
                IsActive = false,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            context.IdentityUsers.Add(inactiveUser);

            context.SaveChanges();
        }
    }
}