using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateLibraryService
{
    private readonly ProcessTemplatePackLoader packLoader;
    private readonly ProcessTemplateProjectionService projectionService;

    public ProcessTemplateLibraryService(
        ProcessTemplatePackLoader packLoader,
        ProcessTemplateProjectionService projectionService)
    {
        this.packLoader = packLoader;
        this.projectionService = projectionService;
    }

    public IReadOnlyList<ProcessTemplateLibraryListItem> ListItems(ProcessTemplateLibraryCategory category)
    {
        var pack = packLoader.Load();

        return category switch
        {
            ProcessTemplateLibraryCategory.Processes => pack.Processes.Values
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildProcessListItem)
                .ToList(),
            ProcessTemplateLibraryCategory.Roles => EnumerateRoleDescriptors(pack)
                .OrderBy(item => item.Resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildRoleListItem)
                .ToList(),
            ProcessTemplateLibraryCategory.Artifacts => EnumerateArtifactDescriptors(pack)
                .OrderBy(item => item.Resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildArtifactListItem)
                .ToList(),
            _ => []
        };
    }

    public ProcessTemplateLibraryPreview GetPreview(ProcessTemplateLibraryCategory category, string itemId)
    {
        var pack = packLoader.Load();

        return category switch
        {
            ProcessTemplateLibraryCategory.Processes => BuildProcessPreview(pack, ResolveProcess(pack, itemId)),
            ProcessTemplateLibraryCategory.Roles => BuildRolePreview(ResolveRole(pack, itemId)),
            ProcessTemplateLibraryCategory.Artifacts => BuildArtifactPreview(ResolveArtifact(pack, itemId)),
            _ => throw new InvalidOperationException($"Unsupported template category '{category}'.")
        };
    }

    public ProcessImportExportEnvelope CreateProcessImportEnvelope(
        string processKey,
        Guid? projectId,
        string? definitionName = null)
    {
        return projectionService.GetProjectedEnvelope(processKey, projectId, definitionName);
    }

    public ProcessRoleEditorModel CreateRoleDraft(string itemId, int ordinal)
    {
        var descriptor = ResolveRole(packLoader.Load(), itemId);
        var keySuffix = ordinal > 1
            ? "-" + ordinal.ToString()
            : string.Empty;

        return new ProcessRoleEditorModel
        {
            Id = Guid.NewGuid(),
            Key = descriptor.Resource.Key + keySuffix,
            DisplayName = descriptor.Resource.DisplayName,
            Purpose = descriptor.Resource.Purpose,
            StaffingIntent = descriptor.Resource.StaffingIntent,
            PreferredExecutorKind = descriptor.Resource.PreferredExecutorKind,
            PreferredProjectAssignmentRole = EnumValueParser.ParseNullable<ProjectPartyAssignmentRole>(descriptor.Resource.PreferredProjectAssignmentRole),
            IsRequired = descriptor.Resource.IsRequired,
            AllowsFallback = descriptor.Resource.AllowsFallback,
            RequiresExplicitApproval = descriptor.Resource.RequiresExplicitApproval,
            DefaultAllocationPercent = descriptor.Resource.DefaultAllocationPercent > 0
                ? descriptor.Resource.DefaultAllocationPercent
                : 100,
            RoleTemplateSourceKey = string.IsNullOrWhiteSpace(descriptor.Resource.RoleTemplateSourceKey)
                ? descriptor.Resource.Key
                : descriptor.Resource.RoleTemplateSourceKey,
            RoleTemplateSnapshotName = descriptor.Resource.RoleTemplateSnapshotName,
            SnapshotSummary = ProcessTemplateRoleSnapshotSummaryBuilder.Build(descriptor.Resource)
        };
    }

    public ProcessArtifactExpectationEditorModel CreateArtifactExpectation(string itemId, bool isRequired = true)
    {
        var descriptor = ResolveArtifact(packLoader.Load(), itemId);

        return new ProcessArtifactExpectationEditorModel
        {
            Id = Guid.NewGuid(),
            ArtifactKind = EnumValueParser.ParseOrDefault(descriptor.Resource.ArtifactKind, ProcessArtifactKind.Evidence),
            Title = descriptor.Resource.DisplayName,
            IsRequired = isRequired,
            TrustRequirement = EnumValueParser.ParseOrDefault(
                descriptor.Resource.DefaultTrustRequirement,
                ProcessArtifactTrustRequirement.ReviewRequired),
            SensitivityLevel = EnumValueParser.ParseOrDefault(
                descriptor.Resource.DefaultSensitivityLevel,
                ProcessSensitivityLevel.Internal),
            RetentionDays = descriptor.Resource.DefaultRetentionDays > 0
                ? descriptor.Resource.DefaultRetentionDays
                : 90,
            AllowedFutureUsageSummary = descriptor.Resource.AllowedFutureUsageSummary,
            ValidationRequirementSummary = descriptor.Resource.ValidationRequirementSummary
        };
    }

    private static ProcessTemplateLibraryListItem BuildProcessListItem(ProcessTemplateDefinition process)
    {
        return new ProcessTemplateLibraryListItem(
            process.Key,
            ProcessTemplateLibraryCategory.Processes,
            process.Key,
            process.DisplayName,
            process.Summary,
            "Process template",
            "Process library",
            string.Empty,
            string.Empty,
            [
                new ProcessTemplateLibraryFact("Criticality", NormalizeValue(process.Criticality, "Standard")),
                new ProcessTemplateLibraryFact("Autonomy", NormalizeValue(process.AutonomyLevel, "Assisted")),
                new ProcessTemplateLibraryFact("Steps", process.Steps.Count.ToString()),
                new ProcessTemplateLibraryFact("Roles", (process.SharedRoleRefs.Count + process.LocalRoleRefs.Count).ToString()),
                new ProcessTemplateLibraryFact("Artifacts", (process.SharedArtifactRefs.Count + process.LocalArtifactRefs.Count).ToString())
            ]);
    }

    private static ProcessTemplateLibraryListItem BuildRoleListItem(RoleDescriptor descriptor)
    {
        return new ProcessTemplateLibraryListItem(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Roles,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared role template" : "Process role template",
            descriptor.IsShared ? "Shared role library" : descriptor.ProcessDisplayName,
            descriptor.ProcessKey,
            descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Executor", NormalizeValue(descriptor.Resource.PreferredExecutorKind, "Not set")),
                new ProcessTemplateLibraryFact("Allocation", descriptor.Resource.DefaultAllocationPercent > 0 ? $"{descriptor.Resource.DefaultAllocationPercent}%" : "Not set"),
                new ProcessTemplateLibraryFact("Approval", descriptor.Resource.RequiresExplicitApproval ? "Explicit" : "Embedded"),
                new ProcessTemplateLibraryFact("Scope", descriptor.IsShared ? "Shared" : "Process-local")
            ]);
    }

    private static ProcessTemplateLibraryListItem BuildArtifactListItem(ArtifactDescriptor descriptor)
    {
        return new ProcessTemplateLibraryListItem(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Artifacts,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared artifact template" : "Process artifact template",
            descriptor.IsShared ? "Shared artifact library" : descriptor.ProcessDisplayName,
            descriptor.ProcessKey,
            descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Kind", NormalizeValue(descriptor.Resource.ArtifactKind, "Evidence")),
                new ProcessTemplateLibraryFact("Owner", NormalizeValue(descriptor.Resource.OwnerRoleKey, "Not set")),
                new ProcessTemplateLibraryFact("Trust", NormalizeValue(descriptor.Resource.DefaultTrustRequirement, "Review required")),
                new ProcessTemplateLibraryFact("Retention", descriptor.Resource.DefaultRetentionDays > 0 ? $"{descriptor.Resource.DefaultRetentionDays} days" : "Not set")
            ]);
    }

    private ProcessTemplateLibraryPreview BuildProcessPreview(ProcessTemplatePack pack, ProcessTemplateDefinition process)
    {
        var roleLinks = BuildProcessRoleLinks(pack, process);
        var artifactLinks = BuildProcessArtifactLinks(pack, process);

        return new ProcessTemplateLibraryPreview(
            process.Key,
            ProcessTemplateLibraryCategory.Processes,
            process.Key,
            process.DisplayName,
            process.Summary,
            "Process template",
            "Process library",
            [
                new ProcessTemplateLibraryFact("Criticality", NormalizeValue(process.Criticality, "Standard")),
                new ProcessTemplateLibraryFact("Autonomy", NormalizeValue(process.AutonomyLevel, "Assisted")),
                new ProcessTemplateLibraryFact("Operating mode", NormalizeValue(process.OperatingMode, "Not set")),
                new ProcessTemplateLibraryFact("Customer", NormalizeValue(process.CustomerName, "Not set")),
                new ProcessTemplateLibraryFact("Owner", NormalizeValue(process.OwnerName, "Not set"))
            ],
            BuildProcessTree(process, roleLinks, artifactLinks),
            BuildDocuments(
                ("definition", "Definition", process.DefinitionMarkdownPath),
                ("compatibility", "Compatibility", process.CurrentModuleCompatibilityReportMarkdownPath)),
            BuildDocuments(
                ("definition-json", "Definition JSON", process.DefinitionJsonPath),
                ("import-envelope", "Import envelope", process.CurrentModuleImportEnvelopePath),
                ("compatibility-json", "Compatibility JSON", process.CurrentModuleCompatibilityReportPath)),
            BuildMermaidDiagrams(
                ("flowchart", "Flowchart", process.FlowchartPath),
                ("sequence", "Sequence", process.SequencePath)),
            roleLinks,
            artifactLinks);
    }

    private static ProcessTemplateLibraryPreview BuildRolePreview(RoleDescriptor descriptor)
    {
        return new ProcessTemplateLibraryPreview(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Roles,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared role template" : "Process role template",
            descriptor.IsShared ? "Shared role library" : descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Executor", NormalizeValue(descriptor.Resource.PreferredExecutorKind, "Not set")),
                new ProcessTemplateLibraryFact("Allocation", descriptor.Resource.DefaultAllocationPercent > 0 ? $"{descriptor.Resource.DefaultAllocationPercent}%" : "Not set"),
                new ProcessTemplateLibraryFact("Scope", descriptor.IsShared ? "Shared" : "Process-local"),
                new ProcessTemplateLibraryFact("Source process", descriptor.IsShared ? "Shared library" : descriptor.ProcessDisplayName),
                new ProcessTemplateLibraryFact("Snapshot", NormalizeValue(descriptor.Resource.RoleTemplateSnapshotName, "Not set"))
            ],
            BuildRoleTree(descriptor),
            BuildDocuments(("role-doc", "Role definition", descriptor.Resource.DocPath)),
            BuildDocuments(("role-json", "Role JSON", ResolveSiblingJsonPath(descriptor.Resource.DocPath))),
            [],
            [],
            []);
    }

    private static ProcessTemplateLibraryPreview BuildArtifactPreview(ArtifactDescriptor descriptor)
    {
        return new ProcessTemplateLibraryPreview(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Artifacts,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared artifact template" : "Process artifact template",
            descriptor.IsShared ? "Shared artifact library" : descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Kind", NormalizeValue(descriptor.Resource.ArtifactKind, "Evidence")),
                new ProcessTemplateLibraryFact("Owner role", NormalizeValue(descriptor.Resource.OwnerRoleKey, "Not set")),
                new ProcessTemplateLibraryFact("Trust", NormalizeValue(descriptor.Resource.DefaultTrustRequirement, "Review required")),
                new ProcessTemplateLibraryFact("Sensitivity", NormalizeValue(descriptor.Resource.DefaultSensitivityLevel, "Internal")),
                new ProcessTemplateLibraryFact("Retention", descriptor.Resource.DefaultRetentionDays > 0 ? $"{descriptor.Resource.DefaultRetentionDays} days" : "Not set")
            ],
            BuildArtifactTree(descriptor),
            BuildDocuments(("artifact-doc", "Artifact definition", descriptor.Resource.DocPath)),
            BuildDocuments(("artifact-json", "Artifact JSON", ResolveSiblingJsonPath(descriptor.Resource.DocPath))),
            [],
            [],
            []);
    }

    private static IReadOnlyList<TreeViewNode> BuildProcessTree(
        ProcessTemplateDefinition process,
        IReadOnlyList<ProcessTemplateLibraryLinkedResource> roles,
        IReadOnlyList<ProcessTemplateLibraryLinkedResource> artifacts)
    {
        return
        [
            new TreeViewNode
            {
                Id = $"process:{process.Key}",
                Text = process.DisplayName,
                Tooltip = process.Summary,
                IsExpanded = true,
                IsSelected = true,
                Children =
                [
                    new TreeViewNode
                    {
                        Id = $"process:{process.Key}:roles",
                        Text = "Roles",
                        BadgeText = roles.Count.ToString(),
                        IsExpanded = true,
                        Children = roles.Select(item => new TreeViewNode
                        {
                            Id = $"role-link:{item.ItemId}",
                            Text = item.Title,
                            Tooltip = item.ScopeLabel
                        }).ToList()
                    },
                    new TreeViewNode
                    {
                        Id = $"process:{process.Key}:artifacts",
                        Text = "Artifacts",
                        BadgeText = artifacts.Count.ToString(),
                        IsExpanded = true,
                        Children = artifacts.Select(item => new TreeViewNode
                        {
                            Id = $"artifact-link:{item.ItemId}",
                            Text = item.Title,
                            Tooltip = item.ScopeLabel
                        }).ToList()
                    },
                    new TreeViewNode
                    {
                        Id = $"process:{process.Key}:steps",
                        Text = "Steps",
                        BadgeText = process.Steps.Count.ToString(),
                        IsExpanded = true,
                        Children = process.Steps
                            .OrderBy(item => item.Order)
                            .Select(step => new TreeViewNode
                            {
                                Id = $"step:{process.Key}:{step.Key}",
                                Text = string.IsNullOrWhiteSpace(step.Title) ? step.Key : step.Title,
                                Tooltip = step.Subtitle,
                                BadgeText = step.StepKind,
                                Children =
                                [
                                    new TreeViewNode
                                    {
                                        Id = $"step:{process.Key}:{step.Key}:roles",
                                        Text = "Role assignments",
                                        BadgeText = step.RoleAssignments.Count.ToString()
                                    },
                                    new TreeViewNode
                                    {
                                        Id = $"step:{process.Key}:{step.Key}:outputs",
                                        Text = "Artifact outputs",
                                        BadgeText = step.ArtifactExpectations.Count.ToString()
                                    },
                                    new TreeViewNode
                                    {
                                        Id = $"step:{process.Key}:{step.Key}:inputs",
                                        Text = "Artifact inputs",
                                        BadgeText = step.ArtifactInputs.Count.ToString()
                                    }
                                ]
                            })
                            .ToList()
                    }
                ]
            }
        ];
    }

    private static IReadOnlyList<TreeViewNode> BuildRoleTree(RoleDescriptor descriptor)
    {
        return
        [
            new TreeViewNode
            {
                Id = $"role:{descriptor.ItemId}",
                Text = descriptor.Resource.DisplayName,
                Tooltip = descriptor.Resource.Summary,
                IsExpanded = true,
                IsSelected = true,
                Children =
                [
                    BuildTreeGroup("knowledge", "Knowledge", descriptor.Resource.KnowledgeRequirements),
                    BuildTreeGroup("experience", "Experience", descriptor.Resource.ExperienceRequirements),
                    BuildTreeGroup("decision-rights", "Decision rights", descriptor.Resource.DecisionRights),
                    BuildTreeGroup("owned-artifacts", "Owned artifacts", descriptor.Resource.OwnedArtifacts),
                    BuildTreeGroup("collaboration", "Collaboration", descriptor.Resource.CollaborationExpectations),
                    BuildTreeGroup("anti-patterns", "Anti-patterns", descriptor.Resource.AntiPatterns),
                    BuildTreeGroup("fitness", "Fitness evidence", descriptor.Resource.FitnessEvidence)
                ]
            }
        ];
    }

    private static IReadOnlyList<TreeViewNode> BuildArtifactTree(ArtifactDescriptor descriptor)
    {
        return
        [
            new TreeViewNode
            {
                Id = $"artifact:{descriptor.ItemId}",
                Text = descriptor.Resource.DisplayName,
                Tooltip = descriptor.Resource.Summary,
                IsExpanded = true,
                IsSelected = true,
                Children =
                [
                    new TreeViewNode
                    {
                        Id = $"artifact:{descriptor.ItemId}:ownership",
                        Text = "Ownership",
                        IsExpanded = true,
                        Children =
                        [
                            new TreeViewNode
                            {
                                Id = $"artifact:{descriptor.ItemId}:owner-role",
                                Text = NormalizeValue(descriptor.Resource.OwnerRoleKey, "Owner role not set")
                            },
                            new TreeViewNode
                            {
                                Id = $"artifact:{descriptor.ItemId}:scope",
                                Text = descriptor.IsShared ? "Shared library" : descriptor.ProcessDisplayName
                            }
                        ]
                    },
                    new TreeViewNode
                    {
                        Id = $"artifact:{descriptor.ItemId}:defaults",
                        Text = "Defaults",
                        IsExpanded = true,
                        Children =
                        [
                            new TreeViewNode
                            {
                                Id = $"artifact:{descriptor.ItemId}:kind",
                                Text = NormalizeValue(descriptor.Resource.ArtifactKind, "Evidence")
                            },
                            new TreeViewNode
                            {
                                Id = $"artifact:{descriptor.ItemId}:trust",
                                Text = NormalizeValue(descriptor.Resource.DefaultTrustRequirement, "Review required")
                            },
                            new TreeViewNode
                            {
                                Id = $"artifact:{descriptor.ItemId}:sensitivity",
                                Text = NormalizeValue(descriptor.Resource.DefaultSensitivityLevel, "Internal")
                            },
                            new TreeViewNode
                            {
                                Id = $"artifact:{descriptor.ItemId}:retention",
                                Text = descriptor.Resource.DefaultRetentionDays > 0
                                    ? $"{descriptor.Resource.DefaultRetentionDays} day retention"
                                    : "Retention not set"
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static TreeViewNode BuildTreeGroup(string key, string title, IReadOnlyList<string> items)
    {
        return new TreeViewNode
        {
            Id = key,
            Text = title,
            BadgeText = items.Count.ToString(),
            IsExpanded = true,
            Children = items.Select((item, index) => new TreeViewNode
            {
                Id = $"{key}:{index}",
                Text = item
            }).ToList()
        };
    }

    private static IReadOnlyList<ProcessTemplateLibraryDocument> BuildDocuments(
        params (string Id, string Title, string Path)[] documentDefinitions)
    {
        var documents = new List<ProcessTemplateLibraryDocument>();
        foreach (var documentDefinition in documentDefinitions)
        {
            var content = ReadOptionalText(documentDefinition.Path);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            documents.Add(new ProcessTemplateLibraryDocument(
                documentDefinition.Id,
                documentDefinition.Title,
                content,
                documentDefinition.Path));
        }

        return documents;
    }

    private static IReadOnlyList<ProcessTemplateLibraryMermaidDiagram> BuildMermaidDiagrams(
        params (string Id, string Title, string Path)[] diagramDefinitions)
    {
        var diagrams = new List<ProcessTemplateLibraryMermaidDiagram>();
        foreach (var diagramDefinition in diagramDefinitions)
        {
            var definition = ReadOptionalText(diagramDefinition.Path);
            if (string.IsNullOrWhiteSpace(definition))
            {
                continue;
            }

            diagrams.Add(new ProcessTemplateLibraryMermaidDiagram(
                diagramDefinition.Id,
                diagramDefinition.Title,
                definition,
                diagramDefinition.Path));
        }

        return diagrams;
    }

    private static IReadOnlyList<ProcessTemplateLibraryLinkedResource> BuildProcessRoleLinks(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process)
    {
        var roles = new List<ProcessTemplateLibraryLinkedResource>();

        foreach (var roleKey in process.SharedRoleRefs)
        {
            if (!pack.SharedRoles.TryGetValue(roleKey, out var role))
            {
                continue;
            }

            roles.Add(new ProcessTemplateLibraryLinkedResource(
                BuildSharedRoleItemId(role.Key),
                role.Key,
                role.DisplayName,
                role.Summary,
                "Shared role library",
                string.Empty,
                "Shared role library"));
        }

        foreach (var roleKey in process.LocalRoleRefs)
        {
            var role = process.LocalRoles.FirstOrDefault(item =>
                string.Equals(item.Key, roleKey, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                continue;
            }

            roles.Add(new ProcessTemplateLibraryLinkedResource(
                BuildLocalRoleItemId(process.Key, role.Key),
                role.Key,
                role.DisplayName,
                role.Summary,
                process.DisplayName,
                process.Key,
                process.DisplayName));
        }

        return roles;
    }

    private static IReadOnlyList<ProcessTemplateLibraryLinkedResource> BuildProcessArtifactLinks(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process)
    {
        var artifacts = new List<ProcessTemplateLibraryLinkedResource>();

        foreach (var artifactKey in process.SharedArtifactRefs)
        {
            if (!pack.SharedArtifacts.TryGetValue(artifactKey, out var artifact))
            {
                continue;
            }

            artifacts.Add(new ProcessTemplateLibraryLinkedResource(
                BuildSharedArtifactItemId(artifact.Key),
                artifact.Key,
                artifact.DisplayName,
                artifact.Summary,
                "Shared artifact library",
                string.Empty,
                "Shared artifact library"));
        }

        foreach (var artifactKey in process.LocalArtifactRefs)
        {
            var artifact = process.LocalArtifacts.FirstOrDefault(item =>
                string.Equals(item.Key, artifactKey, StringComparison.OrdinalIgnoreCase));
            if (artifact is null)
            {
                continue;
            }

            artifacts.Add(new ProcessTemplateLibraryLinkedResource(
                BuildLocalArtifactItemId(process.Key, artifact.Key),
                artifact.Key,
                artifact.DisplayName,
                artifact.Summary,
                process.DisplayName,
                process.Key,
                process.DisplayName));
        }

        return artifacts;
    }

    private static IEnumerable<RoleDescriptor> EnumerateRoleDescriptors(ProcessTemplatePack pack)
    {
        foreach (var role in pack.SharedRoles.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new RoleDescriptor(
                BuildSharedRoleItemId(role.Key),
                role,
                string.Empty,
                "Shared role library",
                true);
        }

        foreach (var process in pack.Processes.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var role in process.LocalRoles.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                yield return new RoleDescriptor(
                    BuildLocalRoleItemId(process.Key, role.Key),
                    role,
                    process.Key,
                    process.DisplayName,
                    false);
            }
        }
    }

    private static IEnumerable<ArtifactDescriptor> EnumerateArtifactDescriptors(ProcessTemplatePack pack)
    {
        foreach (var artifact in pack.SharedArtifacts.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ArtifactDescriptor(
                BuildSharedArtifactItemId(artifact.Key),
                artifact,
                string.Empty,
                "Shared artifact library",
                true);
        }

        foreach (var process in pack.Processes.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var artifact in process.LocalArtifacts.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                yield return new ArtifactDescriptor(
                    BuildLocalArtifactItemId(process.Key, artifact.Key),
                    artifact,
                    process.Key,
                    process.DisplayName,
                    false);
            }
        }
    }

    private static ProcessTemplateDefinition ResolveProcess(ProcessTemplatePack pack, string itemId)
    {
        if (pack.Processes.TryGetValue(itemId, out var process))
        {
            return process;
        }

        throw new InvalidOperationException($"Process template '{itemId}' was not found in the template pack.");
    }

    private static RoleDescriptor ResolveRole(ProcessTemplatePack pack, string itemId)
    {
        const string sharedPrefix = "shared-role:";
        const string localPrefix = "process-role:";

        if (itemId.StartsWith(sharedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var roleKey = itemId[sharedPrefix.Length..];
            if (pack.SharedRoles.TryGetValue(roleKey, out var role))
            {
                return new RoleDescriptor(itemId, role, string.Empty, "Shared role library", true);
            }
        }
        else if (itemId.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = itemId[localPrefix.Length..];
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex > 0)
            {
                var processKey = remainder[..separatorIndex];
                var roleKey = remainder[(separatorIndex + 1)..];
                var process = ResolveProcess(pack, processKey);
                var role = process.LocalRoles.FirstOrDefault(item =>
                    string.Equals(item.Key, roleKey, StringComparison.OrdinalIgnoreCase));
                if (role is not null)
                {
                    return new RoleDescriptor(itemId, role, process.Key, process.DisplayName, false);
                }
            }
        }

        throw new InvalidOperationException($"Role template '{itemId}' was not found in the template pack.");
    }

    private static ArtifactDescriptor ResolveArtifact(ProcessTemplatePack pack, string itemId)
    {
        const string sharedPrefix = "shared-artifact:";
        const string localPrefix = "process-artifact:";

        if (itemId.StartsWith(sharedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var artifactKey = itemId[sharedPrefix.Length..];
            if (pack.SharedArtifacts.TryGetValue(artifactKey, out var artifact))
            {
                return new ArtifactDescriptor(itemId, artifact, string.Empty, "Shared artifact library", true);
            }
        }
        else if (itemId.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = itemId[localPrefix.Length..];
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex > 0)
            {
                var processKey = remainder[..separatorIndex];
                var artifactKey = remainder[(separatorIndex + 1)..];
                var process = ResolveProcess(pack, processKey);
                var artifact = process.LocalArtifacts.FirstOrDefault(item =>
                    string.Equals(item.Key, artifactKey, StringComparison.OrdinalIgnoreCase));
                if (artifact is not null)
                {
                    return new ArtifactDescriptor(itemId, artifact, process.Key, process.DisplayName, false);
                }
            }
        }

        throw new InvalidOperationException($"Artifact template '{itemId}' was not found in the template pack.");
    }

    private static string BuildSharedRoleItemId(string roleKey) => $"shared-role:{roleKey}";

    private static string BuildLocalRoleItemId(string processKey, string roleKey) => $"process-role:{processKey}:{roleKey}";

    private static string BuildSharedArtifactItemId(string artifactKey) => $"shared-artifact:{artifactKey}";

    private static string BuildLocalArtifactItemId(string processKey, string artifactKey) => $"process-artifact:{processKey}:{artifactKey}";

    private static string ResolveSiblingJsonPath(string? docPath)
    {
        if (string.IsNullOrWhiteSpace(docPath))
        {
            return string.Empty;
        }

        var jsonPath = Path.ChangeExtension(docPath, ".json");
        return File.Exists(jsonPath)
            ? jsonPath
            : string.Empty;
    }

    private static string ReadOptionalText(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    private static string NormalizeValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();
        if (normalized.Length == 0)
        {
            return fallback;
        }

        var characters = new List<char>(normalized.Length + 8);
        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            if (index > 0 &&
                char.IsUpper(current) &&
                !char.IsWhiteSpace(normalized[index - 1]) &&
                !char.IsUpper(normalized[index - 1]))
            {
                characters.Add(' ');
            }

            characters.Add(current);
        }

        return new string(characters.ToArray());
    }

    private sealed record RoleDescriptor(
        string ItemId,
        ProcessTemplateRoleResource Resource,
        string ProcessKey,
        string ProcessDisplayName,
        bool IsShared);

    private sealed record ArtifactDescriptor(
        string ItemId,
        ProcessTemplateArtifactResource Resource,
        string ProcessKey,
        string ProcessDisplayName,
        bool IsShared);
}
