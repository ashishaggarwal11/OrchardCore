using System.Linq;
using System.Text.RegularExpressions;
using OrchardCore.ContentManagement.Utilities;

namespace OrchardCore.ReadingTime.Services;

public sealed class ReadingTimeService : IReadingTimeService
{
    private static readonly Regex ScriptTagRegex = new(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleTagRegex = new(@"<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public int? ComputeReadingTimeMinutes(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        // Remove script and style blocks
        var cleaned = ScriptTagRegex.Replace(html, string.Empty);
        cleaned = StyleTagRegex.Replace(cleaned, string.Empty);

        // Strip remaining HTML tags and decode HTML entities
        var text = cleaned.RemoveTags(htmlDecode: true);

        // Split on Unicode whitespace and count non-empty tokens
        var words = Regex.Split(text, @"\s+").Where(w => !string.IsNullOrEmpty(w)).ToArray();
        var wordCount = words.Length;

        // Return null if no readable text
        if (wordCount == 0)
        {
            return null;
        }

        // Calculate reading time: 200 words per minute
        return (int)Math.Ceiling(wordCount / 200.0);
    }
}
