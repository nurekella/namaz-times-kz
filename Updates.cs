using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace NamazTimes;

/// Checks GitHub Releases for a newer tag and installs it in place. The repo ("owner/name") is baked in by CI
/// via -p:GitHubRepo; local dev builds have none and never check.
public static class Updates
{
    static readonly Assembly Asm = typeof(Updates).Assembly;

    public static readonly string? Repo = Asm.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "GitHubRepo")?.Value is { Length: > 0 } r ? r : null;

    public static Version Current => Asm.GetName().Version ?? new(0, 0, 0);

    public record Release(Version Version, string Page, string? MsiUrl, string? ExeUrl);

    static HttpClient Client(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NamazTimesKZ");
        return http;
    }

    public static async Task<Release?> Check()
    {
        if (Repo == null) return null;
        using var http = Client(TimeSpan.FromSeconds(15));
        var rel = await http.GetFromJsonAsync<JsonElement>($"https://api.github.com/repos/{Repo}/releases/latest");
        var tag = rel.GetProperty("tag_name").GetString()!.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var v) || v <= Current) return null;
        string? Asset(Func<string, bool> match) => rel.GetProperty("assets").EnumerateArray()
            .Where(a => match(a.GetProperty("name").GetString()!))
            .Select(a => a.GetProperty("browser_download_url").GetString()).FirstOrDefault();
        return new(v, rel.GetProperty("html_url").GetString()!,
            Asset(n => n.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)),
            Asset(n => n.Equals("NamazTimes.exe", StringComparison.OrdinalIgnoreCase)));
    }

    /// Installed by the MSI (lives under Program Files) → update with the MSI; otherwise it's the portable exe.
    static bool InstalledByMsi => Environment.ProcessPath!.StartsWith(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);

    /// Downloads the new version, then hands over to a hidden PowerShell that waits for this process to exit,
    /// installs (MSI) or swaps the exe (portable), and starts the app again. Caller must exit right after.
    /// ponytail: no signature check — releases are unsigned anyway; verify Authenticode here once they're signed.
    public static async Task Download(Release r, IProgress<int> progress)
    {
        var msi = InstalledByMsi;
        var url = (msi ? r.MsiUrl : r.ExeUrl) ?? throw new InvalidOperationException("asset missing");
        var file = Path.Combine(Path.GetTempPath(), msi ? $"NamazTimes-{r.Version.ToString(3)}.msi" : "NamazTimes-update.exe");

        using var http = Client(TimeSpan.FromMinutes(10));
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? 0;
        await using (var src = await resp.Content.ReadAsStreamAsync())
        await using (var dst = File.Create(file))
        {
            var buf = new byte[81920];
            long done = 0;
            for (int n; (n = await src.ReadAsync(buf)) > 0;)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0) progress.Report((int)(done * 100 / total));
            }
        }

        static string Q(string s) => "'" + s.Replace("'", "''") + "'"; // PowerShell single-quoted literal
        var exe = Environment.ProcessPath!;
        var script = $"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue; " + (msi
            ? $"Start-Process msiexec -ArgumentList '/i',{Q('"' + file + '"')},'/passive' -Wait; "
            : $"Move-Item -Force {Q(file)} {Q(exe)}; ") + $"Start-Process {Q(exe)}";
        Process.Start(new ProcessStartInfo("powershell.exe",
            "-NoProfile -WindowStyle Hidden -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
        { UseShellExecute = false, CreateNoWindow = true });
    }
}
