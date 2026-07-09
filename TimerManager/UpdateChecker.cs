using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TimerManager;

/// <summary>
/// Prüft das neueste GitHub-Release des Projekts und meldet, ob eine neuere
/// Version verfügbar ist. Die Release-Tags entsprechen der GitHub-Run-Nummer,
/// die als Build-Komponente in die Assembly-Version eingebettet wird
/// (siehe Publish-Schritt im Workflow: -p:Version=1.0.&lt;run_number&gt;).
/// </summary>
internal static class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/Moonmancer/Timer/releases/latest";
    private const string ReleasesUrl = "https://github.com/Moonmancer/Timer/releases/latest";

    public record UpdateInfo(int Version, string HtmlUrl);

    /// <summary>Build-Nummer der laufenden App (0 bei lokalen Dev-Builds ohne Versionsstempel).</summary>
    public static int CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.Build ?? 0;

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TimerManager-UpdateCheck");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var stream = await http.GetStreamAsync(ApiUrl);
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagEl)) return null;
            if (!int.TryParse(tagEl.GetString(), out var latest)) return null;

            var url = root.TryGetProperty("html_url", out var urlEl)
                ? urlEl.GetString() ?? ReleasesUrl
                : ReleasesUrl;

            return new UpdateInfo(latest, url);
        }
        catch
        {
            return null;  // offline / API nicht erreichbar → still ignorieren
        }
    }
}
