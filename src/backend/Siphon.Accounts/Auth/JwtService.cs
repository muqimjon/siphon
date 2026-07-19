using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Siphon.Accounts.Data;

namespace Siphon.Accounts.Auth;

public sealed class JwtService(IOptions<AccountsOptions> options)
{
    private readonly AccountsOptions.JwtOptions _jwt = options.Value.Jwt;

    public string Issue(AppUser user)
    {
        var credentials = new SigningCredentials(Key(_jwt.Secret), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim("role", user.Role),
        };
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            expires: DateTime.UtcNow.AddDays(_jwt.AccessTokenDays), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static SymmetricSecurityKey Key(string secret) => new(Encoding.UTF8.GetBytes(secret));

    public static TokenValidationParameters Validation(AccountsOptions.JwtOptions jwt) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = Key(jwt.Secret),
        ValidateLifetime = true,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role",
    };
}
