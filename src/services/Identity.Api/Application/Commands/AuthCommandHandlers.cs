using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Api.Application.DTOs;
using Identity.Api.Application.Exceptions;
using Identity.Api.Configuration;
using Identity.Api.Domain;
using Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Application.Commands;

// WHY: CQRS-lite — RegisterCommandHandler is a dedicated class for one job.
// No business logic leaks into the controller; the handler owns the register flow.
public sealed class RegisterCommandHandler(
    IUserRepository users,
    IPasswordHasher<AppUser> hasher,
    JwtTokenService tokenService)
{
    public async Task<AuthResponse> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        var existing = await users.FindByEmailAsync(request.Email, ct);
        if (existing is not null)
            throw new ConflictException($"Email '{request.Email}' is already registered.");

        var user = new AppUser
        {
            Email = request.Email.ToLowerInvariant(),
            Role = Roles.Customer
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        return tokenService.CreateToken(user);
    }
}

// WHY: Separating login into its own handler makes it easy to add audit logging
// or multi-factor authentication in the future without touching register logic.
public sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher<AppUser> hasher,
    JwtTokenService tokenService)
{
    public async Task<AuthResponse?> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return null;  // Return null so the controller returns a generic failure message.

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        return tokenService.CreateToken(user);
    }
}
