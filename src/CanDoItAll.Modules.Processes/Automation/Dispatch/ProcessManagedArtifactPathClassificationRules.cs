namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagedArtifactPathClassificationRules
{
    public static bool IsLikelyProductSourceOrProjectFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".fsproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".vbproj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".ts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".tsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLikelyProductDeliverableOrSourceFileName(string fileName)
    {
        return ProcessConcreteProductPathRules.IsImplementationDeliverableOrSourceExtension(Path.GetExtension(fileName));
    }

    public static bool IsTextReadableManagedArtifactPath(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               ProcessConcreteProductPathRules.IsCodeOrProjectExtension(extension);
    }

    public static bool IsVisualEvidenceAttachmentPath(string relativePath)
    {
        return IsImageExtension(Path.GetExtension(relativePath));
    }

    public static bool IsResponseProjectableTextArtifact(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageExtension(string extension)
    {
        return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
    }
}

