using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessTemplateLibraryService
{
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
}
