using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ReservationService.Tests;

public static class TestJwtFactory
{
    // Must match ReservationService/appsettings.json Jwt section exactly
    private const string Secret = "dev-only-secret-key-at-least-32-characters-long-change-me";
    private const string Issuer = "LibraryManagementSystem";
    private const string Audience = "LibraryUsers";

    public static string CreateToken(Guid userId, string role)
    {
        var claims = new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}