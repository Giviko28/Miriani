using System.Text.Json;
using Infrastructure.Jira;
using Xunit;

namespace Tests;

public class JiraJqlTests
{
    [Fact]
    public void Empty_search_falls_back_to_the_bounded_default()
    {
        var jql = JiraText.BuildJql("  ", "updated >= -90d order by updated DESC");
        Assert.Equal("updated >= -90d order by updated DESC", jql);
    }

    [Fact]
    public void Search_term_is_wrapped_in_a_bounded_text_query()
    {
        var jql = JiraText.BuildJql("login", "order by updated DESC");
        Assert.Equal("(summary ~ \"login*\" OR text ~ \"login*\") order by updated DESC", jql);
    }

    [Fact]
    public void Double_quotes_and_backslashes_in_search_are_escaped()
    {
        var jql = JiraText.BuildJql("a\"b\\c", "default");
        // The quote and backslash must be escaped so the JQL literal stays well-formed.
        Assert.Contains("a\\\"b\\\\c*", jql);
    }
}

public class JiraAdfTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Flattens_paragraph_text()
    {
        var adf = Parse("""
        { "type": "doc", "content": [
          { "type": "paragraph", "content": [ { "type": "text", "text": "Hello world" } ] }
        ] }
        """);

        Assert.Equal("Hello world", JiraText.FlattenAdf(adf));
    }

    [Fact]
    public void Separates_multiple_paragraphs_and_collapses_blank_runs()
    {
        var adf = Parse("""
        { "type": "doc", "content": [
          { "type": "paragraph", "content": [ { "type": "text", "text": "First" } ] },
          { "type": "paragraph", "content": [ { "type": "text", "text": "Second" } ] }
        ] }
        """);

        var text = JiraText.FlattenAdf(adf);
        Assert.Contains("First", text);
        Assert.Contains("Second", text);
        // No more than one blank line between blocks.
        Assert.DoesNotContain("\n\n\n", text);
    }

    [Fact]
    public void Keeps_list_item_text_and_ignores_marks()
    {
        var adf = Parse("""
        { "type": "doc", "content": [
          { "type": "bulletList", "content": [
            { "type": "listItem", "content": [
              { "type": "paragraph", "content": [
                { "type": "text", "text": "Bold item", "marks": [ { "type": "strong" } ] }
              ] }
            ] }
          ] }
        ] }
        """);

        Assert.Contains("Bold item", JiraText.FlattenAdf(adf));
    }

    [Fact]
    public void Hard_break_becomes_a_newline()
    {
        var adf = Parse("""
        { "type": "paragraph", "content": [
          { "type": "text", "text": "Line one" },
          { "type": "hardBreak" },
          { "type": "text", "text": "Line two" }
        ] }
        """);

        var text = JiraText.FlattenAdf(adf);
        Assert.Equal("Line one\nLine two", text);
    }

    [Fact]
    public void Null_or_empty_description_node_yields_empty_string()
    {
        Assert.Equal("", JiraText.FlattenAdf(Parse("null")));
        Assert.Equal("", JiraText.FlattenAdf(Parse("{}")));
    }
}
