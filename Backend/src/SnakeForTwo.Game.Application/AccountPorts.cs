namespace SnakeForTwo.Game.Application;

public static class UsernameRules
{
    public const int MinLength = 3;

    public const int MaxLength = 24;

    public const string AllowedCharactersDescription =
        "Use 3-24 letters, numbers, underscores, or hyphens.";
}

public sealed record AuthenticatedPlayerIdentity(
    Guid UserId,
    string Username);

public sealed record ExternalLoginProfile(
    string Provider,
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    string? PictureUrl);

public sealed record UserAccount(
    Guid UserId,
    string Username,
    string? Email,
    string? PictureUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LastSignedInAt);

public interface IUserAccountStore
{
    Task<UserAccount> FindOrCreateFromExternalLoginAsync(
        ExternalLoginProfile profile,
        DateTimeOffset signedInAt,
        CancellationToken cancellationToken);

    Task<UserAccount?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<UsernameUpdateResult> UpdateUsernameAsync(
        Guid userId,
        string username,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}

public sealed record UsernameUpdateResult(
    UsernameUpdateStatus Status,
    UserAccount? User,
    string? Message)
{
    public static UsernameUpdateResult Updated(UserAccount user) =>
        new(UsernameUpdateStatus.Updated, user, null);

    public static UsernameUpdateResult NotFound() =>
        new(UsernameUpdateStatus.NotFound, null, "User account was not found.");

    public static UsernameUpdateResult Invalid(string message) =>
        new(UsernameUpdateStatus.Invalid, null, message);

    public static UsernameUpdateResult Unavailable() =>
        new(UsernameUpdateStatus.Unavailable, null, "That username is already taken.");
}

public enum UsernameUpdateStatus
{
    Updated,
    Invalid,
    Unavailable,
    NotFound
}
