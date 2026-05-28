using System.Text;
using System.Text.Json;

namespace Infrastructure.Jira;

/// <summary>
/// Pure text helpers for the Jira client, split out so the fiddly bits — JQL escaping and
/// Atlassian Document Format (ADF) flattening — can be unit-tested without a live Jira.
/// </summary>
public static class JiraText
{
    /// <summary>
    /// Build a safe JQL string from optional free-text search. Jira Cloud's /search/jql endpoint
    /// rejects unbounded queries, so an empty search falls back to the (bounded) default JQL.
    /// </summary>
    public static string BuildJql(string? search, string defaultJql)
    {
        if (string.IsNullOrWhiteSpace(search)) return defaultJql;

        // Escape JQL string literals: backslash first, then double-quote.
        var term = search.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"(summary ~ \"{term}*\" OR text ~ \"{term}*\") order by updated DESC";
    }

    /// <summary>
    /// Wrap plain text into a minimal Atlassian Document Format (ADF) document — one paragraph
    /// per line — which is what the Jira Cloud v3 create-issue API requires for the description.
    /// </summary>
    public static object ToAdf(string text)
    {
        var paragraphs = (text ?? "").Replace("\r\n", "\n").Split('\n')
            .Select(line => new
            {
                type = "paragraph",
                content = string.IsNullOrEmpty(line)
                    ? Array.Empty<object>()
                    : new object[] { new { type = "text", text = line } },
            })
            .Cast<object>()
            .ToArray();

        if (paragraphs.Length == 0)
            paragraphs = new object[] { new { type = "paragraph", content = Array.Empty<object>() } };

        return new { type = "doc", version = 1, content = paragraphs };
    }

    /// <summary>
    /// Walk an ADF tree and concatenate its text. Keeps paragraph/list breaks so the assistant
    /// sees readable prose, ignores styling/marks, and collapses runs of blank lines.
    /// </summary>
    public static string FlattenAdf(JsonElement node)
    {
        var sb = new StringBuilder();
        Walk(node, sb);

        var lines = sb.ToString().Replace("\r\n", "\n").Split('\n');
        var cleaned = new List<string>();
        var blank = false;
        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
            {
                if (!blank && cleaned.Count > 0) cleaned.Add("");
                blank = true;
            }
            else { cleaned.Add(line.TrimEnd()); blank = false; }
        }
        return string.Join("\n", cleaned).Trim();

        static void Walk(JsonElement n, StringBuilder acc)
        {
            if (n.ValueKind != JsonValueKind.Object) return;

            var type = n.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "text" && n.TryGetProperty("text", out var txt))
                acc.Append(txt.GetString());

            if (n.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in content.EnumerateArray()) Walk(child, acc);
            }

            // Block-level nodes end with a line break for readability.
            if (type is "paragraph" or "heading" or "listItem" or "blockquote" or "codeBlock")
                acc.Append('\n');
            else if (type == "hardBreak")
                acc.Append('\n');
        }
    }
}
