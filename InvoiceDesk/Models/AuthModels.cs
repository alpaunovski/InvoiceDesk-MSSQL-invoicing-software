using System;

namespace InvoiceDesk.Models;

public sealed class LoginResponse
{
    public bool Success { get; init; }
    public string Token { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
}

public sealed class ValidateResponse
{
    public bool Valid { get; init; }
    public int? UserId { get; init; }
}

public sealed class SessionToken
{
    public string Token { get; init; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class AuthOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 20;
}
