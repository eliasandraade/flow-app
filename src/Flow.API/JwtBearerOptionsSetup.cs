using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Flow.API;

internal sealed class JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IConfiguration _configuration;

    public JwtBearerOptionsSetup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        var settings = _configuration.GetSection("JwtSettings");
        var secret = settings["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is missing.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = settings["Issuer"],
            ValidateAudience = true,
            ValidAudience = settings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }
}
