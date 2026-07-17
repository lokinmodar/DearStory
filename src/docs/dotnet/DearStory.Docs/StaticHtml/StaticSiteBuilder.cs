using System.Text.Json;
using DearStory.Core;
using DearStory.Docs.Autodocs;
using DearStory.Docs.Markdown;
using Markdig;

namespace DearStory.Docs.StaticHtml;

/// <summary>
/// Builds a safe static HTML site from DearStory Markdown docs and story metadata.
/// </summary>
public sealed class StaticSiteBuilder
{
    private readonly AutodocsGenerator _autodocs = new();

    /// <summary>
    /// Builds the static HTML output for one DearStory workspace.
    /// </summary>
    /// <param name="request">The static site build request.</param>
    /// <param name="cancellationToken">The cancellation token that stops the build.</param>
    public async Task BuildAsync(BuildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(request.OutputDirectory);

        var documents = new List<MarkdownDocumentModel>();
        foreach (var markdownPath in Directory.GetFiles(request.DocsDirectory, "*.md", SearchOption.AllDirectories))
        {
            var markdown = await File.ReadAllTextAsync(markdownPath, cancellationToken).ConfigureAwait(false);
            var document = DocBlockParser.Parse(markdown);
            documents.Add(document);

            var outputName = Path.GetFileNameWithoutExtension(markdownPath) + ".html";
            var outputPath = Path.Combine(request.OutputDirectory, outputName);
            var html = BuildDocumentHtml(document);
            await File.WriteAllTextAsync(outputPath, html, cancellationToken).ConfigureAwait(false);
        }

        var autodocs = _autodocs.Generate(request.Stories);
        var indexHtml = BuildIndexHtml(documents, autodocs);
        await File.WriteAllTextAsync(Path.Combine(request.OutputDirectory, "index.html"), indexHtml, cancellationToken).ConfigureAwait(false);

        var searchData = autodocs.Select(static entry => new { id = entry.StoryId, title = entry.Title }).ToArray();
        await File.WriteAllTextAsync(
            Path.Combine(request.OutputDirectory, "search-data.json"),
            JsonSerializer.Serialize(searchData, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildDocumentHtml(MarkdownDocumentModel document)
    {
        var renderedMarkdown = global::Markdig.Markdown.ToHtml(document.Source);
        var blockSummary = string.Join(
            Environment.NewLine,
            document.Blocks.Select(
                static block =>
                {
                    var attributes = string.Join(
                        " ",
                        block.Attributes.Select(static pair => $"{pair.Key}=\"{pair.Value}\""));
                    return $"<li data-kind=\"{block.Kind}\">{block.Kind} {attributes}</li>";
                }));

        return
            $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <title>{{document.Title}}</title>
            </head>
            <body>
              <main>
                {{renderedMarkdown}}
                <section>
                  <h2>Doc blocks</h2>
                  <ul>
                    {{blockSummary}}
                  </ul>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string BuildIndexHtml(IReadOnlyList<MarkdownDocumentModel> documents, IReadOnlyList<AutodocEntry> autodocs)
    {
        var documentList = string.Join(
            Environment.NewLine,
            documents.Select(static document => $"<li>{document.Title}</li>"));
        var storyList = string.Join(
            Environment.NewLine,
            autodocs.Select(static entry => $"<li data-story-id=\"{entry.StoryId}\">{entry.Title} ({entry.StoryId})</li>"));

        return
            $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <title>DearStory Docs</title>
            </head>
            <body>
              <main>
                <h1>DearStory Docs</h1>
                <section>
                  <h2>Documents</h2>
                  <ul>
                    {{documentList}}
                  </ul>
                </section>
                <section>
                  <h2>Stories</h2>
                  <ul>
                    {{storyList}}
                  </ul>
                </section>
              </main>
            </body>
            </html>
            """;
    }
}

/// <summary>
/// Describes one static-site build request.
/// </summary>
public sealed class BuildRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildRequest" /> class.
    /// </summary>
    /// <param name="docsDirectory">The directory containing Markdown docs to render.</param>
    /// <param name="outputDirectory">The directory where static HTML output should be written.</param>
    /// <param name="stories">The story descriptors available to the docs pipeline.</param>
    public BuildRequest(string docsDirectory, string outputDirectory, IReadOnlyList<StoryDescriptor> stories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(stories);

        DocsDirectory = docsDirectory;
        OutputDirectory = outputDirectory;
        Stories = stories;
    }

    /// <summary>
    /// Gets the directory containing Markdown docs to render.
    /// </summary>
    /// <value>The directory containing Markdown docs to render.</value>
    public string DocsDirectory { get; }

    /// <summary>
    /// Gets the directory where static HTML output should be written.
    /// </summary>
    /// <value>The directory where static HTML output should be written.</value>
    public string OutputDirectory { get; }

    /// <summary>
    /// Gets the story descriptors available to the docs pipeline.
    /// </summary>
    /// <value>The story descriptors available to the docs pipeline.</value>
    public IReadOnlyList<StoryDescriptor> Stories { get; }
}
