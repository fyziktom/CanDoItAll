namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateMermaidExporter
{
    private readonly ProcessTemplatePackLoader packLoader;

    public ProcessTemplateMermaidExporter(ProcessTemplatePackLoader packLoader)
    {
        this.packLoader = packLoader;
    }

    public ProcessTemplateMermaidDocument Export(string processKey)
    {
        var pack = packLoader.Load();
        if (!pack.Processes.TryGetValue(processKey, out var process))
        {
            throw new InvalidOperationException($"Process template '{processKey}' was not found in the template pack.");
        }

        var supportingFiles = BuildSupportingFiles(pack, process);
        return new ProcessTemplateMermaidDocument(
            process.Key,
            process.DisplayName,
            File.ReadAllText(process.FlowchartPath),
            File.ReadAllText(process.SequencePath),
            supportingFiles);
    }

    public IReadOnlyList<string> ExportToFolder(string processKey, string destinationFolder)
    {
        var document = Export(processKey);
        Directory.CreateDirectory(destinationFolder);

        var exportedFiles = new List<string>();
        var flowchartPath = Path.Combine(destinationFolder, "flowchart.mmd");
        var sequencePath = Path.Combine(destinationFolder, "sequence.mmd");

        File.WriteAllText(flowchartPath, document.Flowchart);
        File.WriteAllText(sequencePath, document.Sequence);
        exportedFiles.Add(flowchartPath);
        exportedFiles.Add(sequencePath);

        var pack = packLoader.Load();
        foreach (var relativePath in document.SupportingFiles)
        {
            var sourcePath = Path.Combine(pack.RootPath, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var targetPath = Path.Combine(destinationFolder, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: true);
            exportedFiles.Add(targetPath);
        }

        return exportedFiles;
    }

    private static IReadOnlyList<string> BuildSupportingFiles(ProcessTemplatePack pack, ProcessTemplateDefinition process)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            process.RelativePath + "/definition.md",
            process.RelativePath + "/mermaid/flowchart.mmd",
            process.RelativePath + "/mermaid/sequence.mmd",
            process.RelativePath + "/projection/current-module.compatibility-report.md"
        };

        AddDocs(files, process.DocPath);

        foreach (var roleKey in process.SharedRoleRefs)
        {
            if (pack.SharedRoles.TryGetValue(roleKey, out var role))
            {
                AddDocs(files, role.DocPath);
            }
        }

        foreach (var artifactKey in process.SharedArtifactRefs)
        {
            if (pack.SharedArtifacts.TryGetValue(artifactKey, out var artifact))
            {
                AddDocs(files, artifact.DocPath);
            }
        }

        foreach (var checklistKey in process.SharedChecklistRefs)
        {
            if (pack.SharedChecklists.TryGetValue(checklistKey, out var checklist))
            {
                AddDocs(files, checklist.DocPath);
            }
        }

        foreach (var validationKey in process.SharedValidationRefs)
        {
            if (pack.SharedValidations.TryGetValue(validationKey, out var validation))
            {
                AddDocs(files, validation.DocPath);
            }
        }

        foreach (var promptKey in process.SharedPromptRefs)
        {
            if (pack.SharedPrompts.TryGetValue(promptKey, out var prompt))
            {
                AddDocs(files, prompt.DocPath);
            }
        }

        foreach (var step in process.Steps)
        {
            foreach (var docRef in step.DocRefs)
            {
                AddDocs(files, docRef);
            }
        }

        foreach (var resource in process.LocalRoles)
        {
            AddDocs(files, resource.DocPath);
        }

        foreach (var resource in process.LocalArtifacts)
        {
            AddDocs(files, resource.DocPath);
        }

        foreach (var resource in process.LocalChecklists)
        {
            AddDocs(files, resource.DocPath);
        }

        foreach (var resource in process.LocalValidations)
        {
            AddDocs(files, resource.DocPath);
        }

        foreach (var resource in process.LocalPrompts)
        {
            AddDocs(files, resource.DocPath);
        }

        return files.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddDocs(ICollection<string> files, string relativePath)
    {
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            files.Add(relativePath.Replace("\\", "/"));
        }
    }
}
