namespace Identity.Api.Application.DTOs;

// DTOs live in the Application layer — they are what crosses the HTTP boundary.

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, string Role, Guid UserId);
