namespace SnakeForTwo.Api;

internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string CookieName { get; init; } = "SnakeForTwo.Auth";

    public string? SuccessRedirectUri { get; init; }

    public string? LogoutRedirectUri { get; init; }

    public string? DataProtectionKeysPath { get; init; }
}
