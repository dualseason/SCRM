using Microsoft.Extensions.DependencyInjection;
using SCRM.Services;
using SCRM.Models.Identity;
using Xunit;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace SCRM.TEST.Services
{
    /// <summary>
    /// 简化的认证服务测试
    /// </summary>
    public class AuthServiceTests : IDisposable
    {
        private readonly IJwtService _jwtService;

        public AuthServiceTests()
        {
            // 创建测试服务
            var services = new ServiceCollection();
            services.AddMemoryCache();

            // 添加JWT配置
            var jwtSettings = new SCRM.Configurations.JwtSettings
            {
                SecretKey = "TestSecretKey123456789TestSecretKey123456789",
                Issuer = "SCRM.Test",
                Audience = "SCRM.Test.Clients",
                ExpiryMinutes = 60,
                RefreshTokenExpiryDays = 7
            };
            services.AddSingleton(jwtSettings);

            // 添加模拟的数据库上下文
            services.AddDbContext<SCRM.Data.ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));

            _jwtService = new SCRM.Services.JwtService(
                services.BuildServiceProvider().GetRequiredService<SCRM.Data.ApplicationDbContext>(),
                Microsoft.Extensions.Options.Options.Create(jwtSettings),
                services.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SCRM.Services.JwtService>.Instance
            );
        }

        [Fact]
        public async Task GenerateTokenResponseAsync_ValidUser_ReturnsTokens()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                UserName = "testuser",
                Email = "test@test.com",
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
                PasswordHash = "hashedpassword"
            };

            // Act
            var result = await _jwtService.GenerateTokenResponseAsync(user);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.NotEmpty(result.Token);
            Assert.NotEmpty(result.RefreshToken);
            Assert.Equal("testuser", result.User.UserName);
            Assert.Equal("test@test.com", result.User.Email);
        }

        [Fact]
        public async Task GenerateTokenAsync_ValidUser_ReturnsToken()
        {
            // Arrange
            var user = new User
            {
                Id = 2,
                UserName = "testuser2",
                Email = "test2@test.com",
                FirstName = "Test2",
                LastName = "User2",
                IsActive = true,
                PasswordHash = "hashedpassword2"
            };

            // Act
            var token = await _jwtService.GenerateTokenAsync(user);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);

            // 验证token可以被解析
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            Assert.Equal("2", jwtToken.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal("testuser2", jwtToken.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        }

        [Fact]
        public void ValidateRefreshToken_ValidTokens_ReturnsTrue()
        {
            // Arrange
            var user = new User
            {
                Id = 3,
                UserName = "testuser3",
                Email = "test3@test.com",
                IsActive = true,
                PasswordHash = "hashedpassword3"
            };

            var refreshToken = _jwtService.GenerateRefreshTokenAsync(user);

            // Act
            var result = _jwtService.ValidateRefreshTokenAsync("3", refreshToken);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateRefreshToken_InvalidTokens_ReturnsFalse()
        {
            // Arrange
            var userId = "999";
            var refreshToken = "invalid-refresh-token";

            // Act
            var result = _jwtService.ValidateRefreshTokenAsync(userId, refreshToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RevokeRefreshToken_ValidUserId_DoesNotThrow()
        {
            // Arrange
            var userId = "4";

            // Act & Assert
            var exception = Record.Exception(() => _jwtService.RevokeRefreshTokenAsync(userId));
            Assert.Null(exception);
        }

        [Fact]
        public async Task ValidateToken_ValidToken_ReturnsClaimsPrincipal()
        {
            // Arrange
            var user = new User
            {
                Id = 5,
                UserName = "testuser5",
                Email = "test5@test.com",
                IsActive = true,
                PasswordHash = "hashedpassword5"
            };

            var token = await _jwtService.GenerateTokenAsync(user);

            // Act
            var principal = _jwtService.ValidateTokenAsync(token);

            // Assert
            Assert.NotNull(principal);
            Assert.Equal("5", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            Assert.Equal("testuser5", principal.FindFirst(ClaimTypes.Name)?.Value);
        }

        [Fact]
        public void ValidateToken_InvalidToken_ReturnsNull()
        {
            // Arrange
            var invalidToken = "invalid.token.here";

            // Act
            var principal = _jwtService.ValidateTokenAsync(invalidToken);

            // Assert
            Assert.Null(principal);
        }

        public void Dispose()
        {
            // 清理资源
        }
    }
}