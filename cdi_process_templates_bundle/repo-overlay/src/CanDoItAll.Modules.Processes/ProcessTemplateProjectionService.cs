using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateProjectionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ProcessTemplatePackLoader packLoader;

    public ProcessTemplateProjectionService(ProcessTemplatePackLoader packLoader)
    {
        this.packLoader = packLoader;
    }

    public ProcessImportExportEnvelope GetProjectedEnvelope(
        string processKey,
        Guid? projectId = null,
        string? definitionName = null)
    {
        var process = GetProcess(processKey);
        var envelope = ReadJson<ProcessImportExportEnvelope>(process.CurrentModuleImportEnvelopePath);
        envelope.Definition.Id = null;
        envelope.Definition.WorkingVersionId = null;
        envelope.Definition.ProjectId = projectId;

        if (!string.IsNullOrWhiteSpace(definitionName))
        {
            envelope.Definition.Name = definitionName.Trim();
        }

        if (!envelope.Warnings.Contains($"Projected from template pack process '{process.Key}'.", StringComparer.OrdinalIgnoreCase))
        {
            envelope.Warnings.Add($"Projected from template pack process '{process.Key}'.");
        }

        if (!envelope.Warnings.Contains("Detailed sidecar metadata remains in the process-template pack files.", StringComparer.OrdinalIgnoreCase))
        {
            envelope.Warnings.Add("Detailed sidecar metadata remains in the process-template pack files.");
        }

        if (string.IsNullOrWhiteSpace(envelope.SourceFormat))
        {
            envelope.SourceFormat = "CanDoItAll.ProcessTemplatePack/current-module-projection";
        }

        return envelope;
    }

    public string GetCompatibilityReportJson(string processKey)
    {
        var process = GetProcess(processKey);
        return File.ReadAllText(process.CurrentModuleCompatibilityReportPath);
    }

    public string GetCompatibilityReportMarkdown(string processKey)
    {
        var process = GetProcess(processKey);
        return File.ReadAllText(process.CurrentModuleCompatibilityReportMarkdownPath);
    }

    private ProcessTemplateDefinition GetProcess(string processKey)
    {
        var pack = packLoader.Load();
        if (!pack.Processes.TryGetValue(processKey, out var process))
        {
            throw new InvalidOperationException($"Process template '{processKey}' was not found in the template pack.");
        }

        return process;
    }

    private static T ReadJson<T>(string path)
        where T : class, new()
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? new T();
    }
}
