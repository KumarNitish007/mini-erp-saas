namespace MiniErp.Application.Common;

public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

/// <summary>The request conflicts with current state (duplicate key, concurrent edit...).</summary>
public sealed class ConflictException(string message) : Exception(message);

public sealed class AuthenticationFailedException() : Exception("Invalid email or password.");

public sealed class ForbiddenException(string message) : Exception(message);
