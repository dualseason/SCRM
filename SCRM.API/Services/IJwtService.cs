using Microsoft.IdentityModel.Tokens;
using SCRM.Models.Identity;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface IJwtService
    {
        Task<string> GenerateTokenAsync(User user);
        string GenerateRefreshTokenAsync(User user);
        bool ValidateRefreshTokenAsync(string userId, string refreshToken);
        ClaimsPrincipal? ValidateTokenAsync(string token);
        void RevokeRefreshTokenAsync(string userId);
        Task<TokenResponse> GenerateTokenResponseAsync(User user);
    }

    public class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }
}