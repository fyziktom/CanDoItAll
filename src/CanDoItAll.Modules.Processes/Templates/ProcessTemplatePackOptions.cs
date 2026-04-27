namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplatePackOptions
{
    public const string SectionName = "Processes:TemplatePack";
    public const string TemplatesRootDirectoryName = "Templates";
    public const string ProcessesDirectoryName = "Processes";
    public const string DefaultRelativePackRoot = TemplatesRootDirectoryName + "/" + ProcessesDirectoryName;

    public string PackRoot { get; set; } = string.Empty;
}
