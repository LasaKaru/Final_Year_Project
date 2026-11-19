using System.Text.Json;
using BolaLens.Core.Json;
using BolaLens.Core.Model;

namespace BolaLens.Reporting;

/// <summary>The UI assets (index.html, app.css, app.js) and the offline HTML export: one self-contained file
/// with the CSS, JS and report JSON inlined — no network, no external fonts or scripts (FR16, NFR01).
/// Report text comes from analysed (untrusted) code, so the embedded JSON escapes '&lt;' and the UI renders
/// strings only as text nodes.</summary>
public static class HtmlReportExporter
{
    public static string Asset(string name)
    {
        using var s = typeof(HtmlReportExporter).Assembly.GetManifestResourceStream($"BolaLens.Html.{name}")
                      ?? throw new FileNotFoundException($"UI asset '{name}' not embedded");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static string Export(ScanReport report, object? context, string id)
    {
        var payload = JsonSerializer.Serialize(new { id, report, context }, JsonDefaults.Options)
            .Replace("<", "\\u003c", StringComparison.Ordinal); // cannot close the <script> element
        var html = Asset("index.html")
            .Replace("<title>BolaLens</title>", $"<title>BolaLens report — {Escape(report.Run.TargetPath)}</title>", StringComparison.Ordinal)
            .Replace("<link rel=\"stylesheet\" href=\"app.css\">", "<style>\n" + Asset("app.css") + "\n</style>", StringComparison.Ordinal)
            .Replace("<!--BOLALENS-DATA-->", "<script id=\"bolalens-data\" type=\"application/json\">" + payload + "</script>", StringComparison.Ordinal);
        var js = Asset("app.js").Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
        return html.Replace("<script src=\"app.js\"></script>", "<script>\n" + js + "\n</script>", StringComparison.Ordinal);
    }

    private static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
}
