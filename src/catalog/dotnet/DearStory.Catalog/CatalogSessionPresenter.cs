using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using DearStory.Capture;
using DearStory.Catalog.Capture;
using DearStory.Catalog.Controls;
using DearStory.Catalog.Diagnostics;
using DearStory.Catalog.Preview;
using DearStory.Core;
using DearStory.Core.Schemas;
using ImGuiNET;
using PatchFieldDiagnostic = DearStory.Protocol.Generated.FieldDiagnostic;

namespace DearStory.Catalog;

/// <summary>
/// Coordinates story tree, preview, controls, and diagnostics for one catalog session.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CatalogSessionPresenter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogSessionPresenter" /> class.
    /// </summary>
    /// <param name="catalog">The merged story catalog shared by the active workspace.</param>
    /// <param name="preview">The live preview state for the active story session.</param>
    /// <param name="controls">The schema-control factory used by the catalog UI.</param>
    public CatalogSessionPresenter(StoryCatalog catalog, PreviewFrameState preview, SchemaControlFactory controls)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        Controls = controls ?? throw new ArgumentNullException(nameof(controls));
        CurrentArguments = new JsonObject();
        Diagnostics = Array.Empty<PatchFieldDiagnostic>();
        Tree = CatalogTreeBuilder.Build(Array.Empty<StoryDescriptor>());
        DiagnosticsPanel = new HostDiagnosticsPanel();
        CaptureWorkflow = new CaptureWorkflowState();
    }

    /// <summary>
    /// Gets the capture workflow state for the active catalog session.
    /// </summary>
    /// <value>The capture workflow state for the active catalog session.</value>
    public CaptureWorkflowState CaptureWorkflow { get; }

    /// <summary>
    /// Gets the merged story catalog.
    /// </summary>
    /// <value>The merged story catalog owned by the runner.</value>
    public StoryCatalog Catalog { get; }

    /// <summary>
    /// Gets the active preview state.
    /// </summary>
    /// <value>The active preview state.</value>
    public PreviewFrameState Preview { get; }

    /// <summary>
    /// Gets the schema-control factory used by the catalog.
    /// </summary>
    /// <value>The schema-control factory used by the catalog.</value>
    public SchemaControlFactory Controls { get; }

    /// <summary>
    /// Gets the current catalog tree.
    /// </summary>
    /// <value>The current catalog tree.</value>
    public CatalogTreeNode Tree { get; private set; }

    /// <summary>
    /// Gets the current argument snapshot for the active story.
    /// </summary>
    /// <value>The current argument snapshot for the active story.</value>
    public JsonNode CurrentArguments { get; private set; }

    /// <summary>
    /// Gets the diagnostics produced by the most recent patch attempt.
    /// </summary>
    /// <value>The diagnostics produced by the most recent patch attempt.</value>
    public IReadOnlyList<PatchFieldDiagnostic> Diagnostics { get; private set; }

    /// <summary>
    /// Gets the current schema-derived control descriptor.
    /// </summary>
    /// <value>The current schema-derived control descriptor, or <see langword="null" /> when no story is active.</value>
    public SchemaControlDescriptor? CurrentControl { get; private set; }

    /// <summary>
    /// Gets the diagnostics panel state for the supervised hosts.
    /// </summary>
    /// <value>The diagnostics panel state for the supervised hosts.</value>
    public HostDiagnosticsPanel DiagnosticsPanel { get; }

    /// <summary>
    /// Rebuilds the catalog tree from the supplied story descriptors.
    /// </summary>
    /// <param name="stories">The stories to project into the visible catalog tree.</param>
    public void UpdateStories(IEnumerable<StoryDescriptor> stories)
    {
        Tree = CatalogTreeBuilder.Build(stories);
    }

    /// <summary>
    /// Binds the active argument schema and starting argument snapshot for one story session.
    /// </summary>
    /// <param name="schema">The validated schema for the active story.</param>
    /// <param name="currentArguments">The current serializable argument snapshot.</param>
    public void BindArguments(ArgumentSchema schema, JsonNode currentArguments)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(currentArguments);

        _currentSchema = schema;
        CurrentArguments = currentArguments.DeepClone();
        Diagnostics = Array.Empty<PatchFieldDiagnostic>();
        CurrentControl = Controls.CreateControl(schema);
    }

    /// <summary>
    /// Applies one simple property patch to the active story arguments.
    /// </summary>
    /// <param name="path">The dotted property path to update.</param>
    /// <param name="value">The new JSON value to assign.</param>
    /// <exception cref="InvalidOperationException">No active argument schema has been bound yet.</exception>
    public void ApplyPatch(string path, JsonNode? value)
    {
        if (_currentSchema is null)
        {
            throw new InvalidOperationException("An active story schema must be bound before applying patches.");
        }

        var patch = BuildPatchDocument(path, value);
        var result = ArgumentPatchValidator.Apply(_currentSchema, CurrentArguments, patch);
        CurrentArguments = result.UpdatedArguments;
        Diagnostics = result.Diagnostics;
    }

    /// <summary>
    /// Records the latest host-supervision result in the diagnostics panel.
    /// </summary>
    /// <param name="result">The latest host-supervision result.</param>
    public void RecordHostResult(HostHealthSnapshot result)
    {
        DiagnosticsPanel.Record(result);
    }

    /// <summary>
    /// Records one pending visual capture request initiated from the catalog.
    /// </summary>
    /// <param name="command">The capture request to track.</param>
    public void RequestCapture(CatalogCaptureCommand command)
    {
        CaptureWorkflow.Begin(command);
    }

    /// <summary>
    /// Records one completed visual capture result initiated from the catalog.
    /// </summary>
    /// <param name="result">The completed capture result to track.</param>
    public void CompleteCapture(VisualCaptureResult result)
    {
        CaptureWorkflow.Complete(result);
    }

    /// <summary>
    /// Renders one minimal catalog frame through ImGui.NET.
    /// </summary>
    public void RenderCatalogFrame()
    {
        ImGui.Begin("DearStory Catalog");
        ImGui.Text($"Stories: {CountLeaves(Tree)}");

        if (CurrentControl is not null)
        {
            ImGui.Text($"Control: {CurrentControl.Kind}");
        }

        if (Preview.CurrentFrame is { } frame)
        {
            ImGui.Text($"Preview: seq {frame.Sequence}, {frame.Bytes.Length} bytes");
        }

        RenderNode(Tree);

        if (DiagnosticsPanel.Hosts.Count > 0)
        {
            ImGui.Separator();
            ImGui.Text("Hosts");
            foreach (var host in DiagnosticsPanel.Hosts)
            {
                ImGui.BulletText($"{host.HostId}: {host.State} ({host.RestartAttempts} attempts)");
            }
        }

        ImGui.End();
    }

    private ArgumentSchema? _currentSchema;

    private static JsonNode BuildPatchDocument(string path, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var segments = path
            .Trim()
            .TrimStart('$')
            .TrimStart('.')
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException("The patch path must contain at least one property segment.", nameof(path));
        }

        JsonNode? cursor = value?.DeepClone();
        for (var index = segments.Length - 1; index >= 0; index--)
        {
            cursor = new JsonObject
            {
                [segments[index]] = cursor,
            };
        }

        return cursor ?? new JsonObject();
    }

    private static int CountLeaves(CatalogTreeNode node)
    {
        if (node.Story is not null)
        {
            return 1;
        }

        return node.Children.Sum(CountLeaves);
    }

    private static void RenderNode(CatalogTreeNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Children.Count == 0)
            {
                ImGui.BulletText(child.Title);
                continue;
            }

            if (ImGui.TreeNode(child.Title))
            {
                RenderNode(child);
                ImGui.TreePop();
            }
        }
    }
}
