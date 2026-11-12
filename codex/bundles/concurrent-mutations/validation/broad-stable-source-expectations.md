# Broad Stable source expectations

Prepared on 2026-10-16 before the new Broad Stable discovery or execution.
The current `CanDoItAll.Tests.Stable.slnx` contains **36 assemblies**. Source
accounting expects **18,588 discovery entries and 18,643 executed rows**.
The difference is seven existing nonserializable theories expanding by 55 rows.
These are expectations, not passing test results.

The named trigger is the foundation checkpoint's shared persistence, transaction,
activation/composition, migration and test-support changes. Use the standard
Release source graph and the exact [Testing](../../../../docs/testing.md#broad-stable-gate)
filter:

```text
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

`HostPlatform` and other categories absent from that filter remain included.
The Playwright assembly is not in this solution. No new runner list, test
assembly execution or data-provider invocation supplied these expectations.

## Per-assembly expectations

All names below are assembly/project names. The solution supplies their exact
paths. Repeated or truncated display labels represent separate rows and must
not be deduplicated.

| Assembly | Discovery | Execution |
|---|---:|---:|
| CanDoItAll.AppComponents.Navigation.Tests | 55 | 55 |
| CanDoItAll.Collaboration.Tests | 46 | 46 |
| CanDoItAll.TestLab.Tests | 51 | 51 |
| CanDoItAll.Tests.Unit | 10,251 | 10,285 |
| CanDoItAll.Application.UI.Tests | 28 | 28 |
| CanDoItAll.Workbench.Planning.UI.Tests | 44 | 44 |
| CanDoItAll.Workbench.Insights.UI.Tests | 23 | 23 |
| CanDoItAll.Workbench.Structure.UI.Tests | 7 | 7 |
| CanDoItAll.Workbench.Content.UI.Tests | 45 | 45 |
| CanDoItAll.Workbench.Operators.UI.Tests | 17 | 17 |
| CanDoItAll.Workbench.Execution.UI.Tests | 12 | 12 |
| CanDoItAll.Processes.UI.Tests | 21 | 21 |
| CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests | 68 | 68 |
| CanDoItAll.AgentFramework.WorkflowAuthoring.UI.Tests | 30 | 30 |
| CanDoItAll.Projects.UI.Tests | 62 | 62 |
| CanDoItAll.Projects.Files.UI.Tests | 17 | 17 |
| CanDoItAll.AgentFramework.Editor.UI.Tests | 25 | 25 |
| CanDoItAll.AgentFramework.Providers.UI.Tests | 30 | 30 |
| CanDoItAll.AgentFramework.SharedProviders.UI.Tests | 51 | 51 |
| CanDoItAll.AgentFramework.UI.Tests | 149 | 149 |
| CanDoItAll.Collaboration.UI.Tests | 25 | 25 |
| CanDoItAll.TestLab.UI.Tests | 38 | 38 |
| CanDoItAll.Plugins.UI.Tests | 56 | 56 |
| CanDoItAll.SchedulerPlanner.UI.Tests | 65 | 65 |
| CanDoItAll.Memory.UI.Tests | 42 | 42 |
| CanDoItAll.Resources.UI.Tests | 71 | 71 |
| CanDoItAll.Workspace.UI.Tests | 55 | 55 |
| CanDoItAll.Workspace.ApiAccess.UI.Tests | 85 | 85 |
| CanDoItAll.Workspace.StorageCatalog.UI.Tests | 42 | 42 |
| CanDoItAll.Workspace.StorageRecovery.UI.Tests | 20 | 20 |
| CanDoItAll.Workspace.DataSources.UI.Tests | 27 | 27 |
| CanDoItAll.Workspace.StorageSelection.UI.Tests | 30 | 30 |
| CanDoItAll.Tests.Components | 3,090 | 3,090 |
| CanDoItAll.Tests.Integration | 3,694 | 3,699 |
| CanDoItAll.Memory.Tests | 187 | 203 |
| CanDoItAll.AgentFramework.Memory.Tests | 29 | 29 |
| **Total, 36 assemblies** | **18,588** | **18,643** |

## Derivation and retained evidence

The review used Roslyn 5.0 already loaded by PowerShell 7.6.7 to parse source
syntax only. No compilation was performed. Actual attributed method declarations
were counted; containing-type traits were merged across partial declarations
before applying the canonical filter. This avoids counting sample C# strings
as tests or overlooking a trait declared in another partial file. Ordinary
attribute arithmetic was independently cross-checked against source text.

The retained source inventories are:

- `.artifacts/concurrent-mutations/validation/stable-source-attributes.json`:
  the three monolithic projects, including exact excluded method identities,
  MemberData method identities and candidate abstract/generic/helper types.
- `.artifacts/concurrent-mutations/validation/stable-small-source-attributes.json`:
  the other 33 projects, independently parsed by the same method.
- `.artifacts/concurrent-mutations/validation/stable-source-expectations.json`:
  the per-assembly totals above for the parent-owned comparison.

The source snapshot's application HEAD was
`0bdb050c4326b49aac8d9a7f4ff8b3e8c30229e1`, with the current foundation changes
in the working tree. Counts must be rechecked if test attributes, provider inputs,
test inheritance, compile inclusion, xUnit configuration or the filter change.
Changes only to assertions or the implementation under test do not change this
cardinality expectation, but still require matching built-source proof.

[Unit accounting](broad-stable-unit-source-count.md) derives 5,831 included Facts,
3,907 InlineData rows and 513/547 MemberData rows. It identifies four textual
`[Fact]` specimens that are not methods, all filtered classes/methods, the 23 data
providers and the three deferred expansions totalling 34 additional runtime rows.

[Components accounting](Stable-components-source-accounting.md) derives 1,767
Facts, 1,176 InlineData rows and 147 serializable MemberData rows. No canonical
trait excludes a test there. The linked fixture contains no tests, and the
abstract UI test helper contributes no inherited tests.

The other 33 projects total **1,553 discovery / 1,569 runtime**. Apart from the
following providers, each count is exactly Facts plus InlineData rows. Their
linked compile items are helpers without attributed tests; no compile removal,
conditional test block or additional selected custom Fact changes the counts.

| Assembly/provider | Static derivation | Discovery | Runtime |
|---|---|---:|---:|
| AgentFramework.SharedProviders.UI / SourceRendererTests.Scenarios | 26 `SharingScenario` enum values | 26 | 26 |
| AgentFramework.UI / AgentsCompletionSurfaceTests.Scopes | 3 explicit selections × 5 `ProviderUsagePeriod` values | 15 | 15 |
| AgentFramework.UI / ProviderHistoryScenarioTests.Scenarios | 17 literal `HistoryScenarios.Options` entries | 17 | 17 |
| Memory / MemoryOperationAccessAuthorizerTests.ForeignCallers | 9 explicit custom requester yields | 1 | 9 |
| Memory / MemoryOperationHandlerTests.CrossCallerRoutes, no-provider test | 5 explicit custom caller yields | 1 | 5 |
| Memory / same provider, capability-mismatch test | Same five yields, separate test | 1 | 5 |

The enum/scenario definitions are in SharedProviders.UiSandbox's
`SharedProviderScenarioStore.cs`, AgentFramework.Usage's `ProviderUsageQuery.cs`
and AgentFramework.UiSandbox's `HistoryScenarios.cs`. These serializable rows
are all discovery rows. Memory's ordinary base is 157 Facts + 27 InlineData.
Its one `ExternalCognitiveMemoryFact` carries `LiveProcess` and is excluded.
The two Memory provider files, requester/caller value-type files and test
project are unchanged from `f9dd2b02`; the prior canonical proof confirms
the same 1→9/1→5/1→5 serialization behavior. Their current literal row counts
were separately read. No provider was invoked.

## Integration accounting

The Integration project has 487 local C# files and no linked test source.
There are 1,847 ordinary Facts, 527 Theory methods, 1,821 InlineData rows,
five MemberData methods and four custom live Facts before filtering.
The filter removes 38 ordinary Facts, 32 InlineData rows and all four custom
live Facts. It leaves **1,809 Facts + 1,789 InlineData**. Full excluded method
identities and their class/method traits are in the source inventory. This
includes the two offline activation process cases and the three Projects
performance cases, which retain their dedicated lanes.

| Provider | Static derivation | Discovery | Runtime |
|---|---|---:|---:|
| MafToolAdmissionNativeLoopIntegrationTests.RetainedJournalCases | 3 WireProvider values × 2 streaming choices × 3 literal stages | 18 | 18 |
| PluginCatalogIntegrationTests.BundledPluginSimulationCases | 6 unconditional custom descriptor yields | 1 | 6 |
| WorkflowAuthorizedHitlApiIntegrationTests.OutcomeScenarios | 29 literal TheoryData rows | 29 | 29 |
| WorkflowExternalResponseWebBoundaryTests.OutcomeStatusCases | 28 WorkflowExternalResponseServiceOutcome values | 28 | 28 |
| WorkspaceAgentRecruitingTargetResolverTests.TargetStateCases | 7 ExecutionState + 7 WorkflowRunState + 6 ProcessProjectedRunStatus values | 20 | 20 |
| **MemberData total** | | **96** | **101** |

Thus Integration expects **1,809 + 1,789 + 96 = 3,694 discovery** and
**1,809 + 1,789 + 101 = 3,699 runtime**. All selected rows except the plugin
descriptor use strings, Boolean values, integers or enums. The plugin provider,
its test method and descriptor constructor helper are unchanged from `f9dd2b02`
after line-ending normalization; `WorkflowExecutorModels.cs` is unchanged.
xUnit 2.9.3 and runner 3.1.4 are unchanged. Prior canonical proof confirms that
exact method's 1→6 expansion; the source independently contains six yields.

All Integration test class base lists are fixture/lifetime interfaces rather
than test superclasses. No generic/abstract attributed test class, nonpublic
attributed test method, ClassData, Skip option, disabled discovery enumeration,
conditional test block or assembly-level trait changes this result.

## Historical evidence boundary and execution gate

The retained `.artifacts/cd1-closure-20261011/stable-reconciliation-final.json`
records a valid canonical-filter run: 35 assemblies, 17,379 discovery and 17,434
runtime. It is used only to corroborate the seven unchanged nonserializable
theories, not as the current numerical total. The newer
`.artifacts/ci-repair-38460072674/candidate-v3-stable-reconciled.json` cannot be
the canonical-filter baseline: its `Run-FullCandidate.ps1` passed no filter.
Its 18,050/18,105 counts and five skipped rows must not be reused for this gate.

Parent execution must first build the frozen standard Release source graph
serially, then compare build-backed discovery with all 36 expectations above
using the exact canonical filter. A mismatch is a source/filter/serialization
investigation, never permission to replace an expectation with the runner count.
Retain discovery output, source/dependency identity and assembly hashes before
executing the same binaries/filter. Reconcile all 36 TRX results, expected
theory expansion, failures, skipped/not-executed rows and elapsed time. An
interrupted lane or missing assembly is incomplete proof. The historical audit
script is a useful reference for multiplicity and surrogate display encodings;
do not run it unchanged against its old proof paths or hashes.
