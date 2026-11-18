using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Api.Application.Commands;
using Identity.Api.Application.DTOs;
using Identity.Api.Application.Exceptions;
using Identity.Api.Configuration;
using Identity.Api.Domain;
using Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Identity.Tests.Unit;

public class HandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher<AppUser> _hasher = new PasswordHasher<AppUser>();
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        Secret = "test-secret-key-at-least-32-characters-long!!",
        Issuer = "ShipTrackTest",
        Audience = "ShipTrackClientTest",
        ExpiryMinutes = 15
    });

    private JwtTokenService CreateTokenService() => new(_jwtOptions);

    [Fact]
    public async Task Register_NewCustomer_SuccessfullyCreatesUserAndReturnsToken()
    {
        _userRepo.FindByEmailAsync("new@shiptrack.dev", Arg.Any<CancellationToken>())
            .Returns((AppUser?)null);

        var tokenService = CreateTokenService();
        var handler = new RegisterCommandHandler(_userRepo, _hasher, tokenService);

        var response = await handler.HandleAsync(new RegisterRequest("new@shiptrack.dev", "Secret123"), CancellationToken.None);

        Assert.NotNull(response.Token);
        Assert.Equal(Roles.Customer, response.Role);
        await _userRepo.Received(1).AddAsync(Arg.Is<AppUser>(u => u.Email == "new@shiptrack.dev" && u.Role == Roles.Customer), Arg.Any<CancellationToken>());
        await _userRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ExistingEmail_ThrowsConflictException()
    {
        _userRepo.FindByEmailAsync("existing@shiptrack.dev", Arg.Any<CancellationToken>())
            .Returns(new AppUser { Email = "existing@shiptrack.dev" });

        var handler = new RegisterCommandHandler(_userRepo, _hasher, CreateTokenService());

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new RegisterRequest("existing@shiptrack.dev", "Secret123"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        var user = new AppUser { Email = "user@shiptrack.dev", Role = Roles.Customer };
        user.PasswordHash = _hasher.HashPassword(user, "Password123");

        _userRepo.FindByEmailAsync("user@shiptrack.dev", Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new LoginCommandHandler(_userRepo, _hasher, CreateTokenService());

        var response = await handler.HandleAsync(new LoginRequest("user@shiptrack.dev", "Password123"), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(Roles.Customer, response.Role);
        Assert.Equal(user.Id, response.UserId);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsNull()
    {
        var user = new AppUser { Email = "user@shiptrack.dev" };
        user.PasswordHash = _hasher.HashPassword(user, "CorrectPassword");

        _userRepo.FindByEmailAsync("user@shiptrack.dev", Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new LoginCommandHandler(_userRepo, _hasher, CreateTokenService());

        var response = await handler.HandleAsync(new LoginRequest("user@shiptrack.dev", "WrongPassword"), CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task Login_UserNotFound_ReturnsNull()
    {
        _userRepo.FindByEmailAsync("nonexistent@shiptrack.dev", Arg.Any<CancellationToken>())
            .Returns((AppUser?)null);

        var handler = new LoginCommandHandler(_userRepo, _hasher, CreateTokenService());

        var response = await handler.HandleAsync(new LoginRequest("nonexistent@shiptrack.dev", "Password123"), CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public void JwtTokenService_GeneratesToken_WithRequiredClaims()
    {
        var tokenService = CreateTokenService();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@shiptrack.dev",
            Role = Roles.Admin
        };

        var response = tokenService.CreateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.Token);

        Assert.Equal("ShipTrackTest", jwt.Issuer);
        Assert.Contains("ShipTrackClientTest", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(Roles.Admin, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }
}
