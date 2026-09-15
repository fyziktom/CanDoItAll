namespace CanDoItAll.Modules.Workbench;

// Raised by the asset owner's content validation before any placement or database write. Agent tools classify only
// this typed rejection as a proven no-effect failure; any other failure from a later phase stays uncertain. The UI
// treats it like the other user-correctable asset creation rejections.
public sealed class ProjectAssetContentValidationException : Exception
{
    public ProjectAssetContentValidationException(string message)
        : base(message)
    {
    }

    public ProjectAssetContentValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
