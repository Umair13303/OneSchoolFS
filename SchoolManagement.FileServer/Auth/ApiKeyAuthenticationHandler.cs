using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using FileServerOptions = SchoolManagement.FileServer.Configuration.FileServerOptions;

namespace SchoolManagement.FileServer.Auth;

/// <summary>
/// Options carrier for <see cref="ApiKeyAuthenticationHandler"/>. Nothing to
/// configure per-scheme yet — the shared secret comes from
/// <c>FileServer:ApiKey</c> in appsettings.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Validates the <c>X-Api-Key</c> header against <c>FileServer:ApiKey</c>.
/// When the key matches, the request is authenticated as a synthetic
/// service principal with both the <c>SuperAdmin</c> and <c>Admin</c> roles
/// so it can hit endpoints that the user-facing JWT scheme also protects.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly FileServerOptions _fs;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<FileServerOptions> fileServerOptions)
        : base(options, logger, encoder)
    {
        _fs = fileServerOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No header → let other schemes try (don't fail).
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (string.IsNullOrWhiteSpace(_fs.ApiKey))
        {
            Logger.LogWarning("X-Api-Key sent but FileServer:ApiKey is not configured — rejecting.");
            return Task.FromResult(AuthenticateResult.Fail("API key not configured on server."));
        }

        // Constant-time compare to avoid timing attacks.
        var supplied  = provided.ToString();
        var expected  = _fs.ApiKey;
        if (!CryptographicEquals(supplied, expected))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ServiceAccount:MainAPI"),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("auth_via",      "api-key")
        };
        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
