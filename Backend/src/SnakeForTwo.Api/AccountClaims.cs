using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal static class AccountClaims
{
    public const string UserId = "snakefortwo:user_id";

    public const string Username = "snakefortwo:username";

    public const string HasCustomUsername = "snakefortwo:has_custom_username";

    public const string PictureUrl = "snakefortwo:picture_url";

    public static ClaimsPrincipal CreatePrincipal(UserAccount user) =>
        new(new ClaimsIdentity(CreateClaims(user), CookieAuthenticationDefaults.AuthenticationScheme));

    public static void AddAccountClaims(ClaimsIdentity identity, UserAccount user)
    {
        identity.AddClaim(new Claim(UserId, user.UserId.ToString("D")));
        identity.AddClaim(new Claim(Username, user.DisplayName));
        identity.AddClaim(new Claim(HasCustomUsername, user.HasCustomUsername.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.PictureUrl))
        {
            identity.AddClaim(new Claim(PictureUrl, user.PictureUrl));
        }
    }

    public static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(UserId), out var userId)
            ? userId
            : null;

    public static string? GetUsername(ClaimsPrincipal principal) =>
        principal.FindFirstValue(Username);

    public static bool GetHasCustomUsername(ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirstValue(HasCustomUsername), out var hasCustomUsername) &&
        hasCustomUsername;

    private static IEnumerable<Claim> CreateClaims(UserAccount user)
    {
        yield return new Claim(UserId, user.UserId.ToString("D"));
        yield return new Claim(Username, user.DisplayName);
        yield return new Claim(HasCustomUsername, user.HasCustomUsername.ToString());
        yield return new Claim(ClaimTypes.Name, user.DisplayName);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            yield return new Claim(ClaimTypes.Email, user.Email);
        }

        if (!string.IsNullOrWhiteSpace(user.PictureUrl))
        {
            yield return new Claim(PictureUrl, user.PictureUrl);
        }
    }
}
