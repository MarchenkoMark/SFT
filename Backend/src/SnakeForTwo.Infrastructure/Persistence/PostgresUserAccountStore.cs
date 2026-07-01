using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Infrastructure.Persistence;

public sealed partial class PostgresUserAccountStore(SnakeForTwoDbContext dbContext) : IUserAccountStore
{
    private const int MaxUsernameGenerationAttempts = 100;

    public async Task<UserAccount> FindOrCreateFromExternalLoginAsync(
        ExternalLoginProfile profile,
        DateTimeOffset signedInAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.ProviderUserId);

        var provider = NormalizeProvider(profile.Provider);
        var existingLogin = await dbContext.UserLogins
            .Include(login => login.User)
            .SingleOrDefaultAsync(
                login => login.Provider == provider &&
                    login.ProviderUserId == profile.ProviderUserId,
                cancellationToken);

        if (existingLogin?.User is not null)
        {
            UpdateLogin(existingLogin, profile, signedInAt);
            UpdateSignedInUser(existingLogin.User, profile, signedInAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToAccount(existingLogin.User);
        }

        for (var attempt = 0; attempt < MaxUsernameGenerationAttempts; attempt++)
        {
            var username = CreateUsernameCandidate(profile, attempt);
            var normalizedUsername = NormalizeUsername(username);
            var usernameTaken = await dbContext.UserAccounts.AnyAsync(
                user => user.NormalizedUsername == normalizedUsername,
                cancellationToken);
            if (usernameTaken)
            {
                continue;
            }

            var user = new UserAccountEntity
            {
                Id = Guid.NewGuid(),
                Username = username,
                NormalizedUsername = normalizedUsername,
                Email = NormalizeOptional(profile.Email),
                NormalizedEmail = NormalizeEmail(profile.Email),
                PictureUrl = NormalizeOptional(profile.PictureUrl),
                HasCustomUsername = false,
                CreatedAt = signedInAt,
                UpdatedAt = signedInAt,
                LastSignedInAt = signedInAt
            };
            user.Logins.Add(new UserLoginEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = profile.ProviderUserId,
                Email = NormalizeOptional(profile.Email),
                DisplayName = NormalizeOptional(profile.DisplayName),
                PictureUrl = NormalizeOptional(profile.PictureUrl),
                CreatedAt = signedInAt,
                LastSignedInAt = signedInAt
            });

            dbContext.UserAccounts.Add(user);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return ToAccount(user);
            }
            catch (DbUpdateException exception)
                when (IsUniqueConstraintViolation(exception) && attempt < MaxUsernameGenerationAttempts - 1)
            {
                dbContext.ChangeTracker.Clear();

                var createdByConcurrentRequest = await dbContext.UserLogins
                    .Include(login => login.User)
                    .SingleOrDefaultAsync(
                        login => login.Provider == provider &&
                            login.ProviderUserId == profile.ProviderUserId,
                        cancellationToken);
                if (createdByConcurrentRequest?.User is not null)
                {
                    return ToAccount(createdByConcurrentRequest.User);
                }
            }
        }

        throw new InvalidOperationException("Unable to create a unique username for the signed-in user.");
    }

    public async Task<UserAccount?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.UserAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? null : ToAccount(user);
    }

    public async Task<UsernameUpdateResult> UpdateUsernameAsync(
        Guid userId,
        string username,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var validation = ValidateUsername(username);
        if (validation is not null)
        {
            return UsernameUpdateResult.Invalid(validation);
        }

        var normalizedUsername = NormalizeUsername(username);
        if (normalizedUsername == NormalizeUsername(UsernameRules.GuestDisplayName))
        {
            return UsernameUpdateResult.Invalid("Choose a username other than guest.");
        }

        var user = await dbContext.UserAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return UsernameUpdateResult.NotFound();
        }

        if (user.NormalizedUsername == normalizedUsername)
        {
            user.Username = username.Trim();
            user.HasCustomUsername = true;
            user.UpdatedAt = updatedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
            return UsernameUpdateResult.Updated(ToAccount(user));
        }

        var isTaken = await dbContext.UserAccounts.AnyAsync(
            candidate => candidate.Id != userId &&
                candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);
        if (isTaken)
        {
            return UsernameUpdateResult.Unavailable();
        }

        user.Username = username.Trim();
        user.NormalizedUsername = normalizedUsername;
        user.HasCustomUsername = true;
        user.UpdatedAt = updatedAt;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return UsernameUpdateResult.Updated(ToAccount(user));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return UsernameUpdateResult.Unavailable();
        }
    }

    private static string CreateUsernameCandidate(
        ExternalLoginProfile profile,
        int attempt)
    {
        var baseUsername = CreateBaseUsername(profile);
        var suffix = attempt == 0 ? "" : attempt.ToString(CultureInfo.InvariantCulture);
        var maxBaseLength = UsernameRules.MaxLength - suffix.Length;
        var candidate = $"{baseUsername[..Math.Min(baseUsername.Length, maxBaseLength)]}{suffix}";
        if (candidate.Length < UsernameRules.MinLength)
        {
            candidate = candidate.PadRight(UsernameRules.MinLength, '0');
        }

        return candidate;
    }

    private static string? ValidateUsername(string username)
    {
        var trimmed = username.Trim();
        if (trimmed.Length is < UsernameRules.MinLength or > UsernameRules.MaxLength)
        {
            return UsernameRules.AllowedCharactersDescription;
        }

        return UsernamePattern().IsMatch(trimmed)
            ? null
            : UsernameRules.AllowedCharactersDescription;
    }

    private static string CreateBaseUsername(ExternalLoginProfile profile)
    {
        var source = FirstNonBlank(profile.DisplayName, profile.Email?.Split('@')[0]) ?? "player";
        var builder = new StringBuilder();
        var lastWasSeparator = false;

        foreach (var character in source)
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if ((character == '_' || character == '-' || char.IsWhiteSpace(character)) && !lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        var candidate = builder
            .ToString()
            .Trim('_', '-');
        if (candidate.Length < UsernameRules.MinLength)
        {
            candidate = "player";
        }

        return candidate[..Math.Min(candidate.Length, UsernameRules.MaxLength)];
    }

    private static void UpdateLogin(
        UserLoginEntity login,
        ExternalLoginProfile profile,
        DateTimeOffset signedInAt)
    {
        login.Email = NormalizeOptional(profile.Email);
        login.DisplayName = NormalizeOptional(profile.DisplayName);
        login.PictureUrl = NormalizeOptional(profile.PictureUrl);
        login.LastSignedInAt = signedInAt;
    }

    private static void UpdateSignedInUser(
        UserAccountEntity user,
        ExternalLoginProfile profile,
        DateTimeOffset signedInAt)
    {
        user.Email = NormalizeOptional(profile.Email) ?? user.Email;
        user.NormalizedEmail = NormalizeEmail(profile.Email) ?? user.NormalizedEmail;
        user.PictureUrl = NormalizeOptional(profile.PictureUrl) ?? user.PictureUrl;
        user.LastSignedInAt = signedInAt;
        user.UpdatedAt = signedInAt;
    }

    private static UserAccount ToAccount(UserAccountEntity entity) =>
        new(
            entity.Id,
            entity.Username,
            entity.HasCustomUsername,
            entity.Email,
            entity.PictureUrl,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.LastSignedInAt);

    private static string NormalizeProvider(string provider) =>
        provider.Trim().ToLowerInvariant();

    private static string NormalizeUsername(string username) =>
        username.Trim().ToUpperInvariant();

    private static string? NormalizeEmail(string? email) =>
        NormalizeOptional(email)?.ToUpperInvariant();

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{2,23}$")]
    private static partial Regex UsernamePattern();
}
