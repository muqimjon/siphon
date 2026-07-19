using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Siphon.Media;
using Siphon.Media.Http;

namespace Siphon.Accounts.Auth;

public sealed class ExternalAuthHandlers(UserService users, JwtService jwt, IOptions<AccountsOptions> options)
{
    public const string SignInScheme = "External";

    public IResult Challenge(string scheme, string provider, string? linkToken = null)
    {
        var properties = new AuthenticationProperties { RedirectUri = $"/api/v1/auth/{provider}/callback" };
        if (!string.IsNullOrEmpty(linkToken)) properties.Items["link"] = linkToken;
        return Results.Challenge(properties, [scheme]);
    }

    public async Task<IResult> Callback(HttpContext context, string provider)
    {
        var result = await context.AuthenticateAsync(SignInScheme);
        await context.SignOutAsync(SignInScheme);
        if (!result.Succeeded || result.Principal is null)
            return Problems.Create(ErrorCodes.Unauthorized, "External sign-in failed.");

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var key = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (key is null)
            return Problems.Create(ErrorCodes.Unauthorized, "External provider did not return an identifier.");

        string? linkToken = null;
        result.Properties?.Items.TryGetValue("link", out linkToken);
        var linked = linkToken is null ? null : await users.LinkExternalAsync(linkToken, provider, key);
        if (linkToken is not null && linked is null)
            return Results.Redirect($"{options.Value.WebBaseUrl}/account?link=failed");
        if (linked is not null)
            return Results.Redirect($"{options.Value.WebBaseUrl}/account?link=ok");

        var user = await users.FindOrCreateExternalAsync(provider, key, email);
        var token = jwt.Issue(user);
        return Results.Redirect($"{options.Value.WebBaseUrl}/dashboard#token={token}");
    }
}
