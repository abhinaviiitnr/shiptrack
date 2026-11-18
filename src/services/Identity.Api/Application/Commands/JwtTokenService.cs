using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Api.Application.DTOs;
using Identity.Api.Configuration;
using Identity.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Application.Commands;

// WHY: Extracting JWT creation into its own service makes it reusable
// and testable independently from the login/register flow.
public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opts = options.Value;

    public AuthResponse CreateToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            // jti = unique token ID, useful for token revocation if needed later.
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes),
            signingCredentials: creds);

        return new AuthResponse(
            Token: new JwtSecurityTokenHandler().WriteToken(token),
            Role: user.Role,
            UserId: user.Id);
    }
}
