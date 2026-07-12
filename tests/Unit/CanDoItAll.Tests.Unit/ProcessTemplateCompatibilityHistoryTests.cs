using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessTemplateCompatibilityHistoryTests
{
    [Fact]
    public async Task Template_compatibility_scan_reports_dry_run_sidecar_drift_and_branch_diagnostics()
    {
        var root = CreateTemplatePackRoot();
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            """
            {
              "processes": [
                {
                  "key": "decision-flow",
                  "relativePath": "processes/decision-flow"
                }
              ]
            }
            """);

        var processRoot = Path.Combine(root, "processes", "decision-flow");
        Directory.CreateDirectory(Path.Combine(processRoot, "projection"));
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "definition.json"),
            """
            {
              "kind": "process-template",
              "key": "decision-flow",
              "steps": [
                {
                  "key": "approval",
                  "branchOutcomes": [
                    {
                      "key": "approved",
                      "title": "Approved"
                    }
                  ]
                }
              ]
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "definition.md"),
            "# Generated sidecar");
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "projection", string.Concat("current-module", ".compatibility-report", ".json")),
            """
            {
              "schemaVersion": "process-template-projection-metadata/1.0",
              "projectionKind": "compatibilityReport",
              "sourceJsonHash": "sha256:stale",
              "generatorVersion": "legacy",
              "generatedAtUtc": "2026-06-15T00:00:00Z"
            }
            """);

        var registry = new ProcessTemplateMigrationRegistry(
            [ProcessTemplateCompatibilityScanner.LegacyCurrentModuleSchemaVersion, ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1],
            [new IdentityTemplateMigration(
                "legacy-current-module-to-v1",
                ProcessTemplateCompatibilityScanner.LegacyCurrentModuleSchemaVersion,
                ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1)]);
        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(
            new ProcessTemplateCompatibilityScanRequest(
                root,
                ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
                registry,
                new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(1, report.MigrationDryRun.ProcessCount);
        Assert.Equal(1, report.MigrationDryRun.CanonicalJsonCount);
        Assert.False(report.MigrationDryRun.WouldMutateFiles);
        Assert.Contains(report.SidecarDrift.Sidecars, sidecar => sidecar.Status == ProcessTemplateSidecarDriftStatus.SourceHashMismatch);
        var diagnostic = Assert.Single(report.BranchDiagnostics.Diagnostics);
        Assert.Equal(ProcessBranchMigrationDiagnosticKind.AmbiguousRouteTarget, diagnostic.Kind);
        Assert.True(report.RequiresManualReview);
    }

    [Fact]
    public void Legacy_history_projection_adapter_labels_runs_readonly_and_denies_actions()
    {
        var adapter = new LegacyProcessHistoryProjectionAdapter();
        var legacyRunId = new LegacyProcessRunId("legacy-run-1");
        var records = new[]
        {
            new LegacyProcessRuntimeHistoryRecord(
                legacyRunId,
                LegacyProcessRuntimeRecordKind.Run,
                "ProcessRun",
                new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.Zero),
                ["governanceSnapshot"],
                ProcessProjectedSensitivity.Restricted),
            new LegacyProcessRuntimeHistoryRecord(
                legacyRunId,
                LegacyProcessRuntimeRecordKind.StepRun,
                string.Concat("Process", "StepRun"),
                new DateTimeOffset(2026, 6, 14, 10, 5, 0, TimeSpan.Zero),
                [],
                ProcessProjectedSensitivity.Normal)
        };

        var inventory = adapter.Inventory(records);
        var projection = Assert.Single(adapter.ProjectReadOnlyRuns(records));
        var denial = adapter.DenyRuntimeAction(
            legacyRunId,
            "restart-run",
            "legacy-history://ProcessRun/legacy-run-1");

        Assert.Equal(2, inventory.TotalRecordCount);
        Assert.Equal(2, projection.RecordCount);
        Assert.True(projection.IsReadOnly);
        Assert.Equal(ProcessProjectedSensitivity.Restricted, projection.Sensitivity);
        Assert.Equal(LegacyProcessHistoryActionDenialReason.ReadOnlyLegacyHistory, denial.Reason);
        Assert.Contains("read-only", denial.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compatibility_decision_selects_readonly_legacy_projection_plus_archive_by_default()
    {
        var root = CreateTemplatePackRoot();
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            """
            {
              "processes": []
            }
            """);

        var scannerReport = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(
            new ProcessTemplateCompatibilityScanRequest(
                root,
                ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
                new ProcessTemplateMigrationRegistry([ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1], []),
                new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)));
        var historyAdapter = new LegacyProcessHistoryProjectionAdapter();
        var historyReport = historyAdapter.Inventory(
            [
                new LegacyProcessRuntimeHistoryRecord(
                    new LegacyProcessRunId("legacy-run-2"),
                    LegacyProcessRuntimeRecordKind.Run,
                    "ProcessRun",
                    null,
                    [],
                    ProcessProjectedSensitivity.Normal)
            ]);

        var service = new ProcessCompatibilityDecisionService();
        var decision = service.Decide(
            new ProcessCompatibilityDecisionRequest(
                scannerReport,
                historyReport,
                ProductOwnerApprovedDeletion: false,
                FullMigrationRequired: false,
                SignoffOwner: "process-governance"));

        Assert.Equal(ProcessRuntimeHistoryCompatibilityOption.ReadOnlyLegacyProjectionPlusArchive, decision.SelectedOption);
        Assert.False(decision.AllowsRuntimeActionsOnLegacyRuns);
        Assert.Contains("process-governance", decision.RequiredSignoffOwners);
    }

    [Fact]
    public async Task Template_compatibility_scan_rejects_invalid_manifest_entries()
    {
        var root = CreateTemplatePackRoot();
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            """
            {
              "processes": [
                {
                  "key": "missing-path"
                }
              ]
            }
            """);

        var request = new ProcessTemplateCompatibilityScanRequest(
            root,
            ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
            new ProcessTemplateMigrationRegistry([ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1], []),
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ProcessTemplateCompatibilityScanner.AnalyzeAsync(request));

        Assert.Contains("key and relativePath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_rejects_prose_only_tool_plan()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "dotnet-setup");
        await WriteDefinitionAsync(
            root,
            "dotnet-setup",
            """
            {
              "key": "dotnet-setup",
              "steps": [
                {
                  "key": "create-dotnet-project",
                  "notes": "Run workspace_pwsh_run_script with sideEffectManifest before completion."
                }
              ]
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        Assert.Contains(
            report.TemplateContractDiagnostics.Diagnostics,
            diagnostic =>
                diagnostic.Kind == ProcessTemplateContractDiagnosticKind.ProseOnlyHardGate &&
                diagnostic.ProcessKey == "dotnet-setup" &&
                diagnostic.StepKey == "create-dotnet-project");
        Assert.Contains(
            report.TemplateContractDiagnostics.Diagnostics,
            diagnostic =>
                diagnostic.Kind == ProcessTemplateContractDiagnosticKind.MissingExecutionContract &&
                diagnostic.ProcessKey == "dotnet-setup" &&
                diagnostic.StepKey == "create-dotnet-project");
        Assert.True(report.RequiresManualReview);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_rejects_tool_plan_with_unresolved_script_ref()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "dotnet-setup");
        await WriteDefinitionAsync(
            root,
            "dotnet-setup",
            """
            {
              "key": "dotnet-setup",
              "steps": [
                {
                  "key": "create-dotnet-project",
                  "executionClass": "AgentWithToolPlanGuard",
                  "executionContract": {
                    "executionClass": "AgentWithToolPlanGuard",
                    "deterministicToolPlan": {
                      "planKey": "dotnet-create",
                      "planKind": "DotNetSolutionCreate",
                      "scriptRef": "artifacts/process-runs/{CurrentProcessRunId}/scripts/create.ps1",
                      "operations": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ],
                      "requiredReceipts": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ],
                      "readbackChecks": [
                        {
                          "pathCandidates": [
                            "Calculator.slnx"
                          ],
                          "requiredTextAnyGroups": [
                            [
                              "src/Calculator/Calculator.csproj"
                            ]
                          ]
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        var diagnostic = Assert.Single(
            report.TemplateContractDiagnostics.Diagnostics,
            item => item.Kind == ProcessTemplateContractDiagnosticKind.InvalidDeterministicToolPlan);
        Assert.Contains("{CurrentProcessRunId}", diagnostic.Message, StringComparison.Ordinal);
        Assert.True(report.RequiresManualReview);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_requires_readback_when_typed_plan_flag_is_set()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "generic-mutation");
        await WriteDefinitionAsync(
            root,
            "generic-mutation",
            """
            {
              "key": "generic-mutation",
              "steps": [
                {
                  "key": "mutate-product",
                  "executionClass": "AgentWithToolPlanGuard",
                  "executionContract": {
                    "executionClass": "AgentWithToolPlanGuard",
                    "deterministicToolPlan": {
                      "planKey": "generic-mutation",
                      "planKind": "GenericProductMutation",
                      "requiresReadbackChecks": true,
                      "scriptRef": "artifacts/scripts/mutate.ps1",
                      "operations": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ],
                      "requiredReceipts": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        Assert.Contains(
            report.TemplateContractDiagnostics.Diagnostics,
            diagnostic =>
                diagnostic.Kind == ProcessTemplateContractDiagnosticKind.MissingReadbackChecks &&
                diagnostic.ProcessKey == "generic-mutation" &&
                diagnostic.StepKey == "mutate-product");
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_does_not_infer_readback_from_plan_kind()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "named-plan-kind");
        await WriteDefinitionAsync(
            root,
            "named-plan-kind",
            """
            {
              "key": "named-plan-kind",
              "steps": [
                {
                  "key": "mutate-product",
                  "executionClass": "AgentWithToolPlanGuard",
                  "executionContract": {
                    "executionClass": "AgentWithToolPlanGuard",
                    "deterministicToolPlan": {
                      "planKey": "named-plan-kind",
                      "planKind": "DotNetSolutionCreate",
                      "scriptRef": "artifacts/scripts/mutate.ps1",
                      "operations": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ],
                      "requiredReceipts": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        Assert.DoesNotContain(
            report.TemplateContractDiagnostics.Diagnostics,
            diagnostic => diagnostic.Kind == ProcessTemplateContractDiagnosticKind.MissingReadbackChecks);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_rejects_runtime_owned_subprocess_with_unknown_child_output_step()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "parent-flow", "child-flow");
        await WriteDefinitionAsync(
            root,
            "parent-flow",
            """
            {
              "key": "parent-flow",
              "steps": [
                {
                  "key": "prepare",
                  "stepKind": "Subprocess",
                  "subprocessProcessKey": "child-flow",
                  "executionClass": "RuntimeOwnedSubprocess",
                  "artifactExpectations": [
                    {
                      "key": "handoff",
                      "title": "Handoff"
                    }
                  ],
                  "subprocessContract": {
                    "definitionKey": "child-flow",
                    "parentProducedArtifactExpectationKey": "handoff",
                    "launchMode": "RuntimeOwned",
                    "materializationMode": "RuntimeSynthesizedParentHandoff",
                    "acceptedChildOutputs": [
                      {
                        "stepKey": "missing-step",
                        "artifactExpectationKey": "child-handoff"
                      }
                    ]
                  }
                }
              ]
            }
            """);
        await WriteDefinitionAsync(
            root,
            "child-flow",
            """
            {
              "key": "child-flow",
              "steps": [
                {
                  "key": "actual-step",
                  "artifactExpectations": [
                    {
                      "key": "child-handoff",
                      "title": "Child handoff"
                    }
                  ]
                }
              ]
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        var diagnostic = Assert.Single(
            report.TemplateContractDiagnostics.Diagnostics,
            item => item.Kind == ProcessTemplateContractDiagnosticKind.UnknownSubprocessChildOutputStep);
        Assert.Equal("parent-flow", diagnostic.ProcessKey);
        Assert.Equal("prepare", diagnostic.StepKey);
        Assert.Contains("missing-step", diagnostic.Message, StringComparison.Ordinal);
        Assert.True(report.RequiresManualReview);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_rejects_subprocess_output_with_unknown_parent_branch_route()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "parent-flow", "child-flow");
        await WriteDefinitionAsync(
            root,
            "parent-flow",
            """
            {
              "key": "parent-flow",
              "steps": [
                {
                  "key": "prepare",
                  "stepKind": "Subprocess",
                  "subprocessProcessKey": "child-flow",
                  "executionClass": "RuntimeOwnedSubprocess",
                  "artifactExpectations": [
                    {
                      "key": "handoff",
                      "title": "Handoff"
                    }
                  ],
                  "branchOutcomes": [
                    {
                      "key": "manager-repair",
                      "title": "Manager repair"
                    }
                  ],
                  "subprocessContract": {
                    "definitionKey": "child-flow",
                    "parentProducedArtifactExpectationKey": "handoff",
                    "launchMode": "RuntimeOwned",
                    "materializationMode": "RuntimeSynthesizedParentHandoff",
                    "acceptedChildOutputs": [
                      {
                        "stepKey": "child-handoff",
                        "artifactExpectationKey": "child-handoff-packet",
                        "parentBranchOutcomeKey": "missing-parent-branch"
                      }
                    ]
                  }
                }
              ]
            }
            """);
        await WriteDefinitionAsync(
            root,
            "child-flow",
            """
            {
              "key": "child-flow",
              "steps": [
                {
                  "key": "child-handoff",
                  "artifactExpectations": [
                    {
                      "key": "child-handoff-packet",
                      "title": "Child handoff"
                    }
                  ]
                }
              ]
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        var diagnostic = Assert.Single(
            report.TemplateContractDiagnostics.Diagnostics,
            item => item.Kind == ProcessTemplateContractDiagnosticKind.InvalidBranchOutcomeKey);
        Assert.Equal("parent-flow", diagnostic.ProcessKey);
        Assert.Equal("prepare", diagnostic.StepKey);
        Assert.Contains("missing-parent-branch", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_rejects_file_only_artifact_acceptance_contract()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "artifact-flow");
        await WriteDefinitionAsync(
            root,
            "artifact-flow",
            """
            {
              "key": "artifact-flow",
              "steps": [
                {
                  "key": "write-artifact"
                }
              ]
            }
            """);

        var artifactRoot = Path.Combine(root, "processes", "artifact-flow", "artifacts");
        Directory.CreateDirectory(artifactRoot);
        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "artifact.json"),
            """
            {
              "key": "artifact",
              "semanticAcceptanceContract": {
                "acceptanceMode": "SemanticReview",
                "fileExistenceIsSufficient": true,
                "requiredArtifactSlotKey": "artifact",
                "requiredEvidenceKinds": [
                  "structured-finalizer-output",
                  "reviewed-managed-artifact",
                  "semantic-validation-summary"
                ],
                "requiredReviewSummary": "Must be semantically reviewed."
              }
            }
            """);

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        var diagnostic = Assert.Single(
            report.ArtifactContractDiagnostics.Diagnostics,
            item => item.Kind == ProcessArtifactContractDiagnosticKind.FileOnlyAcceptanceAllowed);
        Assert.Equal("artifact-flow", diagnostic.ProcessKey);
        Assert.Equal("artifact", diagnostic.ArtifactKey);
        Assert.True(report.RequiresManualReview);
    }

    [Fact]
    public async Task Template_loader_materializes_typed_execution_contract()
    {
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "typed-flow");
        await WriteDefinitionAsync(
            root,
            "typed-flow",
            """
            {
              "key": "typed-flow",
              "displayName": "Typed Flow",
              "summary": "Typed",
              "steps": [
                {
                  "key": "create-dotnet-project",
                  "executionClass": "AgentWithToolPlanGuard",
                  "executionContract": {
                    "executionClass": "AgentWithToolPlanGuard",
                    "deterministicToolPlan": {
                      "planKey": "dotnet-create",
                      "planKind": "DotNetSolutionCreate",
                      "scriptRefLaunchVariable": "DotNetCreateProjectScriptRef",
                      "requiresReadbackChecks": true,
                      "operations": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ],
                      "requiredReceipts": [
                        {
                          "key": "run-helper",
                          "toolName": "workspace_pwsh_run_script"
                        }
                      ],
                      "readbackChecks": [
                        {
                          "pathCandidates": [
                            "Calculator.slnx"
                          ],
                          "requiredTextAnyGroups": [
                            [
                              "src/Calculator/Calculator.csproj"
                            ]
                          ]
                        }
                      ]
                    },
                    "requiredReceipts": [
                      {
                        "key": "run-helper",
                        "toolName": "workspace_pwsh_run_script"
                      }
                    ],
                    "producedArtifactSlots": [
                      {
                        "artifactExpectationKey": "setup-evidence",
                        "materializationMode": "RuntimeManaged"
                      }
                    ]
                  },
                  "artifactExpectations": [
                    {
                      "key": "setup-evidence",
                      "title": "Setup Evidence"
                    }
                  ]
                }
              ]
            }
            """);

        var loader = new ProcessTemplatePackLoader(root);
        var definition = loader.LoadDefinition("typed-flow");

        var step = Assert.Single(definition.Steps);
        Assert.Equal(ProcessTemplateStepExecutionClasses.AgentWithToolPlanGuard, step.ExecutionClass);
        Assert.NotNull(step.ExecutionContract);
        Assert.NotNull(step.ExecutionContract.DeterministicToolPlan);
        Assert.Equal("dotnet-create", step.ExecutionContract.DeterministicToolPlan.PlanKey);
        Assert.Equal("DotNetCreateProjectScriptRef", step.ExecutionContract.DeterministicToolPlan.ScriptRefLaunchVariable);
        Assert.True(step.ExecutionContract.DeterministicToolPlan.RequiresReadbackChecks);
        Assert.Single(step.ExecutionContract.RequiredReceipts);
        Assert.Single(step.ExecutionContract.ProducedArtifactSlots);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_accepts_full_migrated_template_pack()
    {
        var root = ProcessTemplatePackLoader.FindPackRoot();

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        Assert.Empty(report.TemplateContractDiagnostics.Diagnostics);
        Assert.Empty(report.ArtifactContractDiagnostics.Diagnostics);
    }

    [Fact]
    public async Task Template_compatibility_strict_scan_rejects_full_pack_when_required_typed_contract_removed()
    {
        var sourceRoot = ProcessTemplatePackLoader.FindPackRoot();
        var root = CreateTemplatePackRoot();
        await WriteManifestAsync(root, "dotnet-solution-setup");
        var processRoot = Path.Combine(root, "processes", "dotnet-solution-setup");
        Directory.CreateDirectory(processRoot);

        var sourceDefinitionPath = Path.Combine(
            sourceRoot,
            "processes",
            "dotnet-solution-setup",
            "definition.json");
        var definition = JsonNode.Parse(await File.ReadAllTextAsync(sourceDefinitionPath))!.AsObject();
        var createProjectStep = definition["Steps"]!
            .AsArray()
            .Select(node => node!.AsObject())
            .Single(step => string.Equals((string?)step["Key"], "create-dotnet-project", StringComparison.Ordinal));
        createProjectStep.Remove("ExecutionContract");
        await File.WriteAllTextAsync(
            Path.Combine(processRoot, "definition.json"),
            definition.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var report = await ProcessTemplateCompatibilityScanner.AnalyzeAsync(CreateStrictScanRequest(root));

        Assert.Contains(
            report.TemplateContractDiagnostics.Diagnostics,
            diagnostic =>
                diagnostic.Kind == ProcessTemplateContractDiagnosticKind.MissingExecutionContract &&
                diagnostic.ProcessKey == "dotnet-solution-setup" &&
                diagnostic.StepKey == "create-dotnet-project");
    }

    [Fact]
    public void Shipped_artifact_templates_declare_semantic_acceptance_contract()
    {
        var root = ProcessTemplatePackLoader.FindPackRoot();
        var files = Directory.GetFiles(Path.Combine(root, "processes"), "*.json", SearchOption.AllDirectories)
            .Where(file => file.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(20, files.Length);
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var contract = document.RootElement.GetProperty("SemanticAcceptanceContract");

            Assert.Equal("SemanticReview", contract.GetProperty("AcceptanceMode").GetString());
            Assert.False(contract.GetProperty("FileExistenceIsSufficient").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(contract.GetProperty("RequiredArtifactSlotKey").GetString()));
            Assert.True(contract.GetProperty("RequiredEvidenceKinds").GetArrayLength() >= 3);
        }
    }

    private static string CreateTemplatePackRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "candoitall-template-compatibility", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static ProcessTemplateCompatibilityScanRequest CreateStrictScanRequest(string root)
        => new(
            root,
            ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1,
            new ProcessTemplateMigrationRegistry(
                [ProcessTemplateCompatibilityScanner.LegacyCurrentModuleSchemaVersion, ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1],
                [new IdentityTemplateMigration(
                    "legacy-current-module-to-v1",
                    ProcessTemplateCompatibilityScanner.LegacyCurrentModuleSchemaVersion,
                    ProcessTemplateSchemaMarker.ProcessDefinitionSchemaV1)]),
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero))
        {
            StrictExecutionContractValidation = true
        };

    private static async Task WriteManifestAsync(string root, params string[] processKeys)
    {
        var entries = string.Join(
            ",",
            processKeys.Select(key =>
                $$"""
                  {
                    "key": "{{key}}",
                    "relativePath": "processes/{{key}}"
                  }
                """));
        await File.WriteAllTextAsync(
            Path.Combine(root, "manifest.json"),
            $$"""
            {
              "processes": [
            {{entries}}
              ]
            }
            """);
    }

    private static async Task WriteDefinitionAsync(string root, string processKey, string definitionJson)
    {
        var processRoot = Path.Combine(root, "processes", processKey);
        Directory.CreateDirectory(processRoot);
        await File.WriteAllTextAsync(Path.Combine(processRoot, "definition.json"), definitionJson);
    }

    private sealed class IdentityTemplateMigration : IProcessTemplateMigration
    {
        public IdentityTemplateMigration(string migrationId, string fromSchemaVersion, string toSchemaVersion)
        {
            MigrationId = migrationId;
            FromSchemaVersion = fromSchemaVersion;
            ToSchemaVersion = toSchemaVersion;
        }

        public string MigrationId { get; }

        public string FromSchemaVersion { get; }

        public string ToSchemaVersion { get; }

        public JsonDocument Migrate(JsonDocument source)
        {
            return JsonDocument.Parse(source.RootElement.GetRawText());
        }
    }
}
