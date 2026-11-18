using Identity.Api.Application.DTOs;
using Identity.Api.Application.Validators;
using Xunit;

namespace Identity.Tests.Unit;

public class ValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();

    [Theory]
    [InlineData("valid@shiptrack.dev", "Password123", true)]
    [InlineData("", "Password123", false)]
    [InlineData("invalid-email", "Password123", false)]
    [InlineData("test@domain.com", "short1", false)] // under 8 chars
    [InlineData("test@domain.com", "nodigitsatall", false)]
    [InlineData("test@domain.com", "12345678", false)] // no letters
    public async Task RegisterValidator_ValidatesCorrectly(string email, string password, bool expectedValid)
    {
        var request = new RegisterRequest(email, password);
        var result = await _registerValidator.ValidateAsync(request);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("user@shiptrack.dev", "Secret123", true)]
    [InlineData("", "Secret123", false)]
    [InlineData("not-an-email", "Secret123", false)]
    [InlineData("user@shiptrack.dev", "", false)]
    public async Task LoginValidator_ValidatesCorrectly(string email, string password, bool expectedValid)
    {
        var request = new LoginRequest(email, password);
        var result = await _loginValidator.ValidateAsync(request);
        Assert.Equal(expectedValid, result.IsValid);
    }
}
