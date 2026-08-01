using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public static partial class ProcessTemplateCompatibilityScanner
{
    private static async Task<IReadOnlyList<ProcessTemplateSidecarDrift>> AnalyzeSidecarsAsync(
        string root,
        string processKey,
        string processRoot,
        string sourceHash,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(processRoot))
        {
            return [];
        }

        var sidecars = new List<ProcessTemplateSidecarDrift>();
        foreach (var path in Directory.EnumerateFiles(processRoot, "*.*", SearchOption.AllDirectories)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(Path.GetFileName(path), "definition.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            var isProjection = IsUnderProjectionDirectory(processRoot, path);
            if (!isProjection &&
                !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".mmd", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sidecars.Add(await AnalyzeSidecarAsync(root, processKey, path, sourceHash, cancellationToken).ConfigureAwait(false));
        }

        return sidecars;
    }

    private static async Task<ProcessTemplateSidecarDrift> AnalyzeSidecarAsync(
        string root,
        string processKey,
        string path,
        string sourceHash,
        CancellationToken cancellationToken)
    {
        var projectionKind = GetProjectionKind(path);
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessTemplateSidecarDrift(
                processKey,
                NormalizeRelative(root, path),
                projectionKind,
                ProcessTemplateSidecarDriftStatus.MissingSourceHash,
                null,
                sourceHash,
                "Generated sidecar does not carry a source JSON hash.");
        }

        try
        {
            using var document = await ReadJsonDocumentAsync(path, cancellationToken).ConfigureAwait(false);
            if (!TryGetString(document.RootElement, "sourceJsonHash", out var sidecarSourceHash))
            {
                return new ProcessTemplateSidecarDrift(
                    processKey,
                    NormalizeRelative(root, path),
                    projectionKind,
                    ProcessTemplateSidecarDriftStatus.MissingSourceHash,
                    null,
                    sourceHash,
                    "Projection JSON does not carry a source JSON hash.");
            }

            var status = string.Equals(sidecarSourceHash, sourceHash, StringComparison.Ordinal)
                ? ProcessTemplateSidecarDriftStatus.Aligned
                : ProcessTemplateSidecarDriftStatus.SourceHashMismatch;
            var message = status == ProcessTemplateSidecarDriftStatus.Aligned
                ? null
                : "Projection source hash does not match canonical definition JSON.";

            return new ProcessTemplateSidecarDrift(
                processKey,
                NormalizeRelative(root, path),
                projectionKind,
                status,
                sidecarSourceHash,
                sourceHash,
                message);
        }
        catch (JsonException ex)
        {
            return new ProcessTemplateSidecarDrift(
                processKey,
                NormalizeRelative(root, path),
                projectionKind,
                ProcessTemplateSidecarDriftStatus.Unreadable,
                null,
                sourceHash,
                ex.Message);
        }
    }

    private static ProcessTemplateProjectionKind GetProjectionKind(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.Equals(Path.GetExtension(path), ".mmd", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessTemplateProjectionKind.Mermaid;
        }

        if (fileName.Contains("compatibility-report", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessTemplateProjectionKind.CompatibilityReport;
        }

        if (fileName.Contains("import-envelope", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessTemplateProjectionKind.ImportEnvelope;
        }

        return ProcessTemplateProjectionKind.Markdown;
    }

    private static bool IsUnderProjectionDirectory(string processRoot, string path)
    {
        var relative = Path.GetRelativePath(processRoot, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "projection", StringComparison.OrdinalIgnoreCase));
    }
}
