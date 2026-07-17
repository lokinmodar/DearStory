using System.Text.RegularExpressions;

namespace DearStory.Docs.Markdown;

/// <summary>
/// Parses CommonMark documents and extracts typed DearStory doc blocks.
/// </summary>
public static class DocBlockParser
{
    private static readonly Regex DocBlockPattern = new(
        @"^:::(?<kind>[A-Za-z0-9\-]+)(?<attributes>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AttributePattern = new(
        @"(?<name>[A-Za-z0-9\-]+)=""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses one Markdown document and extracts typed doc blocks.
    /// </summary>
    /// <param name="markdown">The Markdown source text to parse.</param>
    /// <returns>The parsed Markdown document model.</returns>
    public static MarkdownDocumentModel Parse(string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);

        var blocks = new List<MarkdownDocBlock>();
        string? title = null;

        foreach (var rawLine in markdown.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (title is null && line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
            }

            if (line == ":::" || !line.StartsWith(":::", StringComparison.Ordinal))
            {
                continue;
            }

            var match = DocBlockPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attributeMatch in AttributePattern.Matches(match.Groups["attributes"].Value))
            {
                attributes[attributeMatch.Groups["name"].Value] = attributeMatch.Groups["value"].Value;
            }

            blocks.Add(
                new MarkdownDocBlock
                {
                    Kind = match.Groups["kind"].Value,
                    Attributes = attributes,
                });
        }

        return new MarkdownDocumentModel
        {
            Title = title ?? "Untitled",
            Source = markdown,
            Blocks = blocks,
        };
    }
}
