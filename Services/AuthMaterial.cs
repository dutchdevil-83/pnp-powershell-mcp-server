using System.Text;

namespace PnPPowerShell.MCPServer.Services;

/// <summary>Legacy auth inventory fields retained for serialized diagnostics and transcript compatibility.</summary>
internal sealed class PersistedLogin
{
    public string? Url { get; set; }

    public string? ClientId { get; set; }

    public bool Enabled { get; set; }
}

internal sealed class PersistedLoginStore
{
    public List<PersistedLogin>? Cache { get; set; }
}

/// <summary>What this machine can authenticate with. The legacy module exposes no supported credential inventory.</summary>
internal sealed record AuthFacts(
    IReadOnlyList<PersistedLogin> PersistedLogins,
    bool TokenCachePresent,
    string? ClientIdVariable,
    string? ClientId,
    string? CertificatePath,
    string? StoreError)
{
    public static AuthFacts None { get; } = new([], false, null, null, null, null);
}

internal sealed record SetupStep(string Command, bool UserRuns, string Why)
{
    public string Summary => UserRuns
        ? $"Ask the user to run this in Windows PowerShell 5.1, then run 'pnp_diagnose_connection' again: {Command} ({Why})"
        : $"Run: {Command} ({Why})";
}

/// <summary>Provides a legacy-compatible connection suggestion without inspecting or exposing credentials.</summary>
internal static class AuthMaterial
{
    private const string UrlPlaceholder = "https://<tenant>.sharepoint.com/sites/<site>";

    public static AuthFacts Gather(string? storeDirectory = null) => AuthFacts.None;

    public static string? TenantOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var text = url.Contains("://", StringComparison.Ordinal) ? url.Trim() : "https://" + url.Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var parsed) && parsed.Host.Length > 0
            ? parsed.Host.Split('.')[0].ToLowerInvariant()
            : null;
    }

    public static IReadOnlyList<SetupStep> Render(StringBuilder report, AuthFacts facts, string? targetUrl)
    {
        report.AppendLine("4. Authentication");
        report.AppendLine("   The legacy module does not expose a supported inventory of cached credentials or app registrations.");
        report.AppendLine("   Do not assume modern PnP.PowerShell persisted logins, environment variables, or registration cmdlets apply here.");

        var url = string.IsNullOrWhiteSpace(targetUrl) ? UrlPlaceholder : targetUrl.Trim();
        report.AppendLine("   For SharePoint Online, the legacy interactive option is -UseWebLogin. Confirm the target and authentication method with the user before connecting.");

        return
        [
            new SetupStep(
                $"Connect-PnPOnline -Url {url} -UseWebLogin",
                UserRuns: false,
                "this uses the legacy SharePointPnPPowerShellOnline interactive login in this persistent session; browser interaction may be required"),
        ];
    }
}
