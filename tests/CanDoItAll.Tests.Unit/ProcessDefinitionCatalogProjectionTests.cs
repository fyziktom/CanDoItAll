using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDefinitionCatalogProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Catalog_query_filters_definitions_and_selects_requested_item()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var selectedKey = new ProcessDefinitionCatalogItemKey("architecture-review");

        var catalog = await service.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogQueryProjection("architecture", selectedKey, ProcessDefinitionCatalogScopeKind.All, Take: 20));

        var item = Assert.Single(catalog.Items);
        Assert.Equal(selectedKey, item.Key);
        Assert.Equal(selectedKey, catalog.SelectedDefinitionKey);
        Assert.Equal("Architecture review", catalog.SelectedItem?.Name);
        Assert.Equal(2, catalog.PublishedDefinitionCount);
        Assert.Contains(catalog.ScopeGroups, group => group.ScopeKind == ProcessDefinitionCatalogScopeKind.Global && group.Count == 2);
    }

    [Fact]
    public async Task Feed_defaults_returns_command_receipt_and_refresh_token()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var receipt = await service.FeedDefaultDefinitionsAsync(
            new ProcessDefinitionFeedDefaultsCommand(ProcessWorkspaceShellScope.Global));

        Assert.Equal(ProcessDefinitionCatalogCommandStatus.Accepted, receipt.Status);
        Assert.Equal(ProcessDefinitionCatalogCommandKind.FeedDefaults, receipt.CommandKind);
        Assert.Equal(2, receipt.AffectedDefinitionCount);
        Assert.StartsWith("feed-defaults:test-pack:", receipt.RefreshToken.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Loader_rejects_manifest_definition_key_mismatch()
    {
        using var pack = TemporaryProcessTemplatePack.Create(
            ("manifest-key", "definition-key", "Mismatched definition", "Mismatch summary"));
        var loader = new ProcessTemplatePackLoader(pack.RootPath);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("does not match manifest key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editor_projection_reads_authoring_sections_from_template_metadata()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        Assert.Equal("Architecture review", editor.Identity.Name);
        Assert.Equal("Architecture owner", editor.Identity.OwnerName);
        Assert.Equal(ProcessDefinitionCriticalityLevel.High, editor.Governance.Criticality);
        Assert.Equal(ProcessDefinitionAutonomyLevel.Guarded, editor.Governance.AutonomyLevel);
        Assert.Equal(ProcessDefinitionOperatingModeKind.GovernedLive, editor.Governance.OperatingMode);
        Assert.Equal("Manager override.", editor.Governance.ManagerOverrideSummary);
        Assert.Equal("Interface contract.", editor.Contracts.InterfaceContractSummary);
        Assert.Equal(1, editor.Simulation.StepCount);
        Assert.Equal(1, editor.Simulation.RequiredRoleCount);
        Assert.Equal(1, editor.Simulation.RequiredArtifactExpectationCount);
        Assert.False(editor.Lint.HasBlockingIssues);
    }

    [Fact]
    public async Task Role_editor_projection_reads_roles_templates_and_step_bindings()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var role = Assert.Single(editor.Roles);
        var templateAction = Assert.Single(editor.TemplateActions);
        var binding = Assert.Single(editor.StepRoleBindings);
        Assert.Equal(new ProcessDefinitionRoleKey("solution-architect"), role.RoleKey);
        Assert.Equal("Solution architect", role.DisplayName);
        Assert.Equal(ProcessDefinitionRoleExecutorKind.PersonOrAgent, role.Draft.PreferredExecutorKind);
        Assert.Equal(ProcessDefinitionRoleProjectAssignmentKind.Architect, role.Draft.PreferredProjectAssignmentRole);
        Assert.True(role.Draft.IsRequired);
        Assert.True(role.Draft.AllowsFallback);
        Assert.True(role.Draft.RequiresExplicitApproval);
        Assert.Equal("process-role-template/solution-architect", role.Draft.RoleTemplateSourceKey);
        Assert.Equal(ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate, role.Draft.OverrideStatus);
        Assert.Equal(new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"), templateAction.ActionKey);
        Assert.Equal(ProcessDefinitionRoleExecutorKind.PersonOrAgent, templateAction.PreferredExecutorKind);
        Assert.Equal(role.RoleKey, binding.RoleKey);
        Assert.Equal(ProcessStepRoleResponsibilityKind.Approver, binding.ResponsibilityKind);
        Assert.False(editor.Lint.HasBlockingIssues);
        Assert.All(editor.Commands, command => Assert.True(command.IsEnabled));
    }

    [Fact]
    public async Task Role_editor_save_rejects_invalid_executor_or_allocation()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var invalidDraft = editor.SelectedRole!.Draft with
        {
            PreferredExecutorKind = ProcessDefinitionRoleExecutorKind.Unspecified,
            DefaultAllocationPercent = 101
        };

        var result = await service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionRoleCommandKind.SaveRole,
            editor.VersionToken,
            invalidDraft,
            TemplateActionKey: null));

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, result.Receipt.Status);
        Assert.True(result.Projection.Lint.HasBlockingIssues);
        Assert.Contains(result.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.execution.executor-required");
        Assert.Contains(result.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.execution.allocation-out-of-range");
    }

    [Fact]
    public async Task Role_editor_add_apply_save_and_delete_follow_typed_command_boundary()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var templateActionKey = editor.TemplateActions.Single().ActionKey;

        var added = await ExecuteRoleCommandAsync(
            service,
            editor,
            ProcessDefinitionRoleCommandKind.AddRole,
            editor.SelectedRole!.Draft,
            templateActionKey);
        var savedDraft = added.Projection.SelectedRole!.Draft with
        {
            DisplayName = "Principal architecture steward",
            PreferredProjectAssignmentRole = ProcessDefinitionRoleProjectAssignmentKind.Manager
        };
        var saved = await ExecuteRoleCommandAsync(
            service,
            added.Projection,
            ProcessDefinitionRoleCommandKind.SaveRole,
            savedDraft,
            templateActionKey: null);
        var applied = await ExecuteRoleCommandAsync(
            service,
            saved.Projection,
            ProcessDefinitionRoleCommandKind.ApplyTemplate,
            saved.Projection.SelectedRole!.Draft,
            templateActionKey);
        var deleted = await ExecuteRoleCommandAsync(
            service,
            applied.Projection,
            ProcessDefinitionRoleCommandKind.DeleteRole,
            applied.Projection.SelectedRole!.Draft,
            templateActionKey: null);

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, added.Receipt.Status);
        Assert.Equal(2, added.Projection.Roles.Count);
        Assert.Equal("Principal architecture steward", saved.Projection.SelectedRole?.DisplayName);
        Assert.Equal(ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate, applied.Projection.SelectedRole?.Draft.OverrideStatus);
        Assert.DoesNotContain(deleted.Projection.Roles, role => role.RoleKey == applied.Projection.SelectedRole!.RoleKey);
    }

    [Fact]
    public async Task Role_editor_rejects_stale_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var templateActionKey = editor.TemplateActions.Single().ActionKey;
        var added = await ExecuteRoleCommandAsync(
            service,
            editor,
            ProcessDefinitionRoleCommandKind.AddRole,
            editor.SelectedRole!.Draft,
            templateActionKey);

        var staleAdd = await service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionRoleCommandKind.AddRole,
            editor.VersionToken,
            editor.SelectedRole.Draft,
            templateActionKey));
        var staleSave = await service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            added.Projection.DefinitionKey,
            ProcessDefinitionRoleCommandKind.SaveRole,
            editor.VersionToken,
            added.Projection.SelectedRole!.Draft,
            TemplateActionKey: null));

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, staleAdd.Receipt.Status);
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, staleSave.Receipt.Status);
        Assert.Contains(staleAdd.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.version-conflict");
        Assert.Contains(staleSave.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.version-conflict");
    }

    [Fact]
    public async Task Publish_rejects_blocking_lint_and_returns_actionable_projection()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var invalidDraft = CreateDraft(editor) with
        {
            Identity = editor.Identity with { Name = string.Empty }
        };

        var result = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Publish,
            editor.VersionToken,
            invalidDraft));

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, result.Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandKind.Publish, result.Receipt.CommandKind);
        Assert.True(result.Projection.Lint.HasBlockingIssues);
        Assert.Contains(result.Projection.Lint.Issues, issue =>
            issue.Code == "processes.definition.identity.name-required" &&
            issue.Severity == ProcessDefinitionEditorLintSeverity.Error);
        Assert.Equal(string.Empty, result.Projection.Identity.Name);
    }

    [Fact]
    public async Task Save_publish_archive_and_delete_follow_typed_status_transitions()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var saved = await ExecuteEditorCommandAsync(service, editor, ProcessDefinitionEditorCommandKind.SaveDraft);
        var published = await ExecuteEditorCommandAsync(service, saved.Projection, ProcessDefinitionEditorCommandKind.Publish);
        var archived = await ExecuteEditorCommandAsync(service, published.Projection, ProcessDefinitionEditorCommandKind.Archive);
        var deleted = await ExecuteEditorCommandAsync(service, archived.Projection, ProcessDefinitionEditorCommandKind.Delete);

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, saved.Receipt.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, saved.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Published, published.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Archived, archived.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.TemplateDefault, deleted.Projection.Status);
        Assert.Contains("template default remains available", deleted.Receipt.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Archive_and_delete_reject_stale_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var saved = await ExecuteEditorCommandAsync(service, editor, ProcessDefinitionEditorCommandKind.SaveDraft);
        var staleDraft = CreateDraft(saved.Projection);

        var archived = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Archive,
            editor.VersionToken,
            staleDraft));
        var deleted = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Delete,
            editor.VersionToken,
            staleDraft));

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, archived.Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, deleted.Receipt.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, archived.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, deleted.Projection.Status);
        Assert.Contains(archived.Projection.Lint.Issues, issue => issue.Code == "processes.definition.version-conflict");
        Assert.Contains(deleted.Projection.Lint.Issues, issue => issue.Code == "processes.definition.version-conflict");
    }

    private static Task<ProcessDefinitionEditorCommandResult> ExecuteEditorCommandAsync(
        ProcessDefinitionEditorProjectionService service,
        ProcessDefinitionEditorProjection editor,
        ProcessDefinitionEditorCommandKind commandKind)
        => service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            commandKind,
            editor.VersionToken,
            CreateDraft(editor)));

    private static Task<ProcessDefinitionRoleEditorCommandResult> ExecuteRoleCommandAsync(
        ProcessDefinitionRoleEditorProjectionService service,
        ProcessDefinitionRoleEditorProjection editor,
        ProcessDefinitionRoleCommandKind commandKind,
        ProcessDefinitionRoleDraftProjection draft,
        ProcessDefinitionRoleTemplateActionKey? templateActionKey)
        => service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            commandKind,
            editor.VersionToken,
            draft,
            templateActionKey));

    private static ProcessDefinitionEditorDraftProjection CreateDraft(
        ProcessDefinitionEditorProjection editor)
        => new(
            editor.DefinitionKey,
            editor.Identity,
            editor.Governance,
            editor.Contracts,
            editor.Simulation);

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TemporaryProcessTemplatePack : IDisposable
    {
        private TemporaryProcessTemplatePack(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static TemporaryProcessTemplatePack CreateDefault()
            => Create(
                ("delivery-default", "delivery-default", "Delivery default", "Default delivery flow"),
                ("architecture-review", "architecture-review", "Architecture review", "Architecture governance flow"));

        public static TemporaryProcessTemplatePack Create(
            params (string ManifestKey, string DefinitionKey, string DisplayName, string Summary)[] definitions)
        {
            var root = Directory.CreateTempSubdirectory("process-template-pack-").FullName;
            var processes = definitions
                .Select(definition => $$"""
                    {
                      "Key": "{{definition.ManifestKey}}",
                      "RelativePath": "processes/{{definition.ManifestKey}}"
                    }
                    """)
                .ToArray();
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                $$"""
                {
                  "PackKey": "test-pack",
                  "Name": "Test process template pack",
                  "Version": "test-pack",
                  "GeneratedAtUtc": "2026-06-16T00:00:00Z",
                  "Processes": [
                    {{string.Join("," + Environment.NewLine, processes)}}
                  ]
                }
                """);

            foreach (var definition in definitions)
            {
                var directory = Path.Combine(root, "processes", definition.ManifestKey);
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    Path.Combine(directory, "definition.json"),
                    $$"""
                    {
                      "Kind": "process-template-definition",
                      "Key": "{{definition.DefinitionKey}}",
                      "DisplayName": "{{definition.DisplayName}}",
                      "Summary": "{{definition.Summary}}",
                      "ValueStatement": "Template value statement.",
                      "CustomerName": "Architecture customer",
                      "OwnerName": "Architecture owner",
                      "InterfaceContractSummary": "Interface contract.",
                      "ManagerOverrideSummary": "Manager override.",
                      "GovernanceNotes": "Governance notes.",
                      "ChangeSummary": "Change summary.",
                      "GovernancePolicySummary": "Governance policy.",
                      "ConstitutionRuleSummary": "Constitution rule.",
                      "OperatingModeSummary": "Operating mode summary.",
                      "SimulationReadinessSummary": "Simulation readiness.",
                      "Criticality": "High",
                      "OperatingMode": "GovernedLive",
                      "AutonomyLevel": "Guarded",
                      "RoleUsages": [
                        {
                          "Key": "solution-architect",
                          "RoleResourceKey": "solution-architect",
                          "DisplayName": "Solution architect",
                          "Purpose": "Own architecture decisions and technical tradeoffs.",
                          "StaffingIntent": "Assign a senior architecture owner before launch planning.",
                          "PreferredExecutorKind": "person-or-agent",
                          "PreferredProjectAssignmentRole": "Architect",
                          "IsRequired": true,
                          "AllowsFallback": true,
                          "RequiresExplicitApproval": true,
                          "DefaultAllocationPercent": 60,
                          "RoleTemplateSourceKey": "process-role-template/solution-architect",
                          "RoleTemplateSnapshotName": "Solution architect v1",
                          "SnapshotSummary": "Architecture role template snapshot.",
                          "Notes": "Coordinates architecture choices."
                        }
                      ],
                      "Steps": [
                        {
                          "Key": "architecture-decision",
                          "Title": "Architecture decision",
                          "RoleAssignments": [
                            {
                              "RoleKey": "solution-architect",
                              "ResponsibilityKind": "Approver",
                              "IsRequired": true,
                              "FallbackOrder": 1,
                              "RebindPolicySummary": "Rebind to the architecture board when the primary owner is unavailable."
                            }
                          ],
                          "ArtifactExpectations": [
                            {
                              "IsRequired": true
                            }
                          ]
                        }
                      ]
                    }
                    """);
            }

            Directory.CreateDirectory(Path.Combine(root, "toolbox"));
            File.WriteAllText(
                Path.Combine(root, "toolbox", "role-templates.json"),
                """
                [
                  {
                    "ActionId": "role-template.solution-architect",
                    "Label": "Solution architect template",
                    "Summary": "Owns architecture decisions and technical tradeoffs.",
                    "TemplateRoleKey": "solution-architect",
                    "KeyPrefix": "solution-architect",
                    "DisplayNameTemplate": "Solution architect {ordinal}",
                    "PreferredExecutorKind": "person-or-agent",
                    "DefaultAllocationPercent": 60
                  }
                ]
                """);

            return new TemporaryProcessTemplatePack(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
