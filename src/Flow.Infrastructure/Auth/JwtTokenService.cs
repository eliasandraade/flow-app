using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Flow.Application.Common.Interfaces;
using Flow.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Flow.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private static readonly JwtSecurityTokenHandler _tokenHandler;

    static JwtTokenService()
    {
        _tokenHandler = new JwtSecurityTokenHandler();
        _tokenHandler.InboundClaimTypeMap.Clear();
    }

    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(User user, IList<string> roles)
    {
        var settings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.Parse(settings["ExpiryMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: settings["Issuer"],
            audience: settings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public Guid? GetUserIdFromToken(string accessToken)
    {
        var settings = _configuration.GetSection("JwtSettings");
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["SecretKey"]!)),
            ValidateIssuer = true,
            ValidIssuer = settings["Issuer"],
            ValidateAudience = true,
            ValidAudience = settings["Audience"],
            ValidateLifetime = false  // allows extracting userId from expired tokens (refresh flow)
        };

        try
        {
            var principal = _tokenHandler.ValidateToken(accessToken, validationParams, out _);
            var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
