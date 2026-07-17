using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DearStory.Sdk.Generator;

/// <summary>
/// Reads normalized summary text from Roslyn XML documentation payloads.
/// </summary>
internal static class XmlDocumentationReader
{
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Reads the normalized summary text from one XML documentation payload.
    /// </summary>
    /// <param name="xml">The XML documentation payload.</param>
    /// <returns>The normalized summary text, or <see langword="null" /> when none is available.</returns>
    public static string? ReadSummary(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(xml);
            var summary = document.Descendants("summary").FirstOrDefault();
            if (summary is null)
            {
                return null;
            }

            var normalized = WhitespacePattern.Replace(summary.Value, " ").Trim();
            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }
        catch
        {
            return null;
        }
    }
}
