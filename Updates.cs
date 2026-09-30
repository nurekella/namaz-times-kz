using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace NamazTimes;

/// Checks GitHub Releases for a newer tag. The repo ("owner/name") is baked in by CI via -p:GitHubRepo;
/// local dev builds have none and never check.
public static class Updates
{
    static readonly Assembly Asm = typeof(Updates).Assembly;

    public static readonly string? Repo = Asm.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "GitHubRepo")?.Value is { Length: > 0 } r ? r : null;

    public static Version Current => Asm.GetName().Version ?? new(0, 0, 0);

    public static async Task<(Version Version, string Url)?> Check()
    {
        if (Repo == null) return null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NamazTimesKZ");
        var rel = await http.GetFromJsonAsync<JsonElement>($"https://api.github.com/repos/{Repo}/releases/latest");
        var tag = rel.GetProperty("tag_name").GetString()!.TrimStart('v', 'V');
        return Version.TryParse(tag, out var v) && v > Current ? (v, rel.GetProperty("html_url").GetString()!) : null;
    }
}
