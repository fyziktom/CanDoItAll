using System.Text;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public sealed class PromptFactoryCreateActionDeduplicator
{
    private readonly TimeSpan duplicateWindow;
    private string lastSignature = string.Empty;
    private DateTimeOffset lastProcessedAt = DateTimeOffset.MinValue;

    public PromptFactoryCreateActionDeduplicator(TimeSpan? duplicateWindow = null)
    {
        this.duplicateWindow = duplicateWindow ?? TimeSpan.FromMilliseconds(450);
    }

    public bool ShouldProcess(CanvasWorkbenchCreateActionRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signature = BuildSignature(request);
        if (string.Equals(signature, lastSignature, StringComparison.Ordinal) &&
            now - lastProcessedAt <= duplicateWindow)
        {
            return false;
        }

        lastSignature = signature;
        lastProcessedAt = now;
        return true;
    }

    private static string BuildSignature(CanvasWorkbenchCreateActionRequest request)
    {
        var builder = new StringBuilder();
        builder.Append(request.ActionId);
        builder.Append('|');
        builder.Append(request.SourceNodeId);
        builder.Append('|');
        builder.Append(request.ParentNodeId);
        builder.Append('|');
        builder.Append(request.PlacementKind);
        builder.Append('|');
        builder.Append(request.ObjectSubtype);
        builder.Append('|');
        builder.Append(request.Title?.Trim());
        builder.Append('|');
        builder.Append(request.Subtitle?.Trim());
        builder.Append('|');
        builder.Append(request.Notes?.Trim());
        builder.Append('|');
        builder.Append(request.UploadedFile?.FileName);

        foreach (var inputValue in request.InputValues ?? [])
        {
            builder.Append('|');
            builder.Append(inputValue.Key);
            builder.Append('=');
            builder.Append(inputValue.Value?.Trim());
        }

        return builder.ToString();
    }
}
