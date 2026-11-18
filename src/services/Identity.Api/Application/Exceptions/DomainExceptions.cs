// WHY: A custom exception type lets the global handler translate domain errors
// into the right HTTP status codes without try/catch in every handler.
namespace Identity.Api.Application.Commands;

public sealed class ConflictException(string message) : Exception(message);
