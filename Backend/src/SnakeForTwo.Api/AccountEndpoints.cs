using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool accountsEnabled)
    {
        if (!accountsEnabled)
        {
            endpoints.MapGet("/auth/login/google", AccountsDisabled);
            endpoints.MapGet("/auth/me", AccountsDisabled);
            endpoints.MapPut("/auth/me/username", AccountsDisabled);
            endpoints.MapPost("/auth/logout", AccountsDisabled);
            return endpoints;
        }

        endpoints.MapGet("/auth/login/google", LoginWithGoogle);
        endpoints.MapGet("/auth/me", GetCurrentUserAsync);
        endpoints.MapPut("/auth/me/username", UpdateUsernameAsync);
        endpoints.MapPost("/auth/logout", LogoutAsync);
        return endpoints;
    }

    private static IResult AccountsDisabled() =>
        Results.Problem(
            title: "Accounts are disabled.",
            detail: "Configure PostgreSQL persistence before enabling account sign-in.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static IResult LoginWithGoogle(
        string? returnUrl,
        IConfiguration configuration)
    {
        if (!IsGoogleConfigured(configuration))
        {
            return Results.Problem(
                title: "Google sign-in is not configured.",
                detail: "Configure Authentication:Google:ClientId and Authentication:Google:ClientSecret.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var redirectUri = ResolveFrontendRedirectUri(
            returnUrl,
            configuration,
            "Auth:SuccessRedirectUri");

        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        IUserAccountStore users,
        CancellationToken cancellationToken)
    {
        var userId = AccountClaims.GetUserId(context.User);
        if (userId is null)
        {
            return Results.Ok(AccountResponse.SignedOut);
        }

        var user = await users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(AccountResponse.SignedOut);
        }

        return Results.Ok(AccountResponse.SignedIn(user));
    }

    private static async Task<IResult> UpdateUsernameAsync(
        HttpContext context,
        UsernameUpdateRequest request,
        IUserAccountStore users,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = AccountClaims.GetUserId(context.User);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await users.UpdateUsernameAsync(
            userId.Value,
            request.Username,
            timeProvider.GetUtcNow(),
            cancellationToken);

        switch (result.Status)
        {
            case UsernameUpdateStatus.Updated:
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    AccountClaims.CreatePrincipal(result.User!),
                    new AuthenticationProperties
                    {
                        AllowRefresh = true,
                        IsPersistent = true
                    });
                return Results.Ok(AccountResponse.SignedIn(result.User!));

            case UsernameUpdateStatus.Invalid:
                return Results.BadRequest(new
                {
                    error = "invalidUsername",
                    message = result.Message
                });

            case UsernameUpdateStatus.Unavailable:
                return Results.Conflict(new
                {
                    error = "usernameUnavailable",
                    message = result.Message
                });

            case UsernameUpdateStatus.NotFound:
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NotFound(new
                {
                    error = "userNotFound",
                    message = result.Message
                });

            default:
                return Results.Problem("Unexpected username update result.");
        }
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IConfiguration configuration)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new
        {
            isAuthenticated = false,
            redirectUri = ResolveFrontendRedirectUri(
                null,
                configuration,
                "Auth:LogoutRedirectUri")
        });
    }

    private static bool IsGoogleConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    private static string ResolveFrontendRedirectUri(
        string? requestedUri,
        IConfiguration configuration,
        string configuredFallbackKey)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        var configuredFallback = configuration[configuredFallbackKey];
        var fallback = FirstAbsoluteUri(configuredFallback, allowedOrigins.FirstOrDefault()) ?? "/";

        if (string.IsNullOrWhiteSpace(requestedUri))
        {
            return fallback;
        }

        var trimmed = requestedUri.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) &&
            IsAllowedHttpUri(absolute, allowedOrigins))
        {
            return absolute.ToString();
        }

        if (trimmed.StartsWith("/", StringComparison.Ordinal) &&
            Uri.TryCreate(fallback, UriKind.Absolute, out var fallbackAbsolute))
        {
            return new Uri(fallbackAbsolute.GetLeftPart(UriPartial.Authority) + trimmed).ToString();
        }

        return fallback;
    }

    private static bool IsAllowedHttpUri(Uri uri, IReadOnlyList<string> allowedOrigins)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return allowedOrigins.Any(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var allowed) &&
            string.Equals(uri.Scheme, allowed.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.Host, allowed.Host, StringComparison.OrdinalIgnoreCase) &&
            uri.Port == allowed.Port);
    }

    private static string? FirstAbsoluteUri(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate =>
            Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https");

    private sealed record AccountResponse(
        bool IsAuthenticated,
        AccountUserResponse? User)
    {
        public static AccountResponse SignedOut { get; } = new(false, null);

        public static AccountResponse SignedIn(UserAccount user) =>
            new(true, new AccountUserResponse(
                user.UserId,
                user.Username,
                user.Email,
                user.PictureUrl));
    }

    private sealed record AccountUserResponse(
        Guid UserId,
        string Username,
        string? Email,
        string? PictureUrl);
}

internal sealed record UsernameUpdateRequest(string Username);
