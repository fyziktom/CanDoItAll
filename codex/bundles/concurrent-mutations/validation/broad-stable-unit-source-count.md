# Broad Stable Unit source expectation

Prepared before the new runner list, 2026-10-16. This is source accounting for
`tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`, not test execution.
No test assembly or data provider was invoked to obtain these counts.

**Expected discovery: 10,251. Expected execution: 10,285 rows.**

The canonical `docs/testing.md` filter is:

```text
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

## Method and trait census

The independent Roslyn source census associates method attributes with their
containing class, including partial-class attributes. Its retained inventory is
`.artifacts/concurrent-mutations/validation/stable-source-attributes.json`.

| Source category | All source | Excluded by filter | Included |
|---|---:|---:|---:|
| Facts | 6,175 | 344 | 5,831 |
| Theory methods | 1,015 | 26 | 989 |
| InlineData rows | 4,002 | 95 | 3,907 |
| MemberData methods | 23 | 0 | 23 |

The exclusions are the twelve UnixRuntimePortability classes and two methods
with RequiresHostDocker=true. HostPlatform, UnixPortabilityCore, Scale and the
other unexcluded categories remain included. No ClassData, custom Fact/Theory/Data
attribute, Skip setting, disabled discovery enumeration or assembly-level trait
changes the Unit count. All nineteen test classes with a base list implement only
IDisposable; none inherits test methods from another class. The generic types in
the census are helpers, not test classes. The project has default compile items
and no additional linked test source or explicit compile removal.

The raw textual count of 6,179 `[Fact]` strings is incorrect: four occur inside
source specimens at `HostPlatformTestClassificationTests.cs:67`, `:77`, `:87` and
`:105`. Roslyn identifies 6,175 actual Fact attributes. This distinction is not a
test exclusion or a discovered-count adjustment.

## MemberData accounting

Each row below names the containing class and provider. Paths are under
`tests/Unit/CanDoItAll.Tests.Unit` unless another owner is stated. The table counts
data from source without invoking its provider. Enums, Type values and byte arrays
in these cases are serializable by the existing xUnit runner; the three explicitly
identified custom-value cases retain their existing deferred expansion.

| Class/provider | Source derivation | Discovery | Execution |
|---|---|---:|---:|
| `AgentFramework/CapabilitiesSandboxContextTests.Scenarios` | 29 entries in UiSandbox `CapabilitiesSandboxContext.Scenarios` | 29 | 29 |
| `AgentFramework/OverviewSandboxContextTests.Scenarios` | 18 entries in UiSandbox `OverviewSandboxContext.Scenarios` | 18 | 18 |
| `AgentFramework/GovernanceSandboxTests.Scenarios` | 14 GovernanceSandboxScenario enum members | 14 | 14 |
| `AgentFramework/ProviderSharedReconciliationTests.EditableCases` | 8 SharedProviderChangeKind members × two booleans | 16 | 16 |
| `AgentToolInvocationPolicyTests.ProcessMutationTools` | Ten explicit string constants | 10 | 10 |
| `AgentToolInvocationPolicyTests.ProcessReadTools` | Thirteen explicit string constants | 13 | 13 |
| `CrmHr/CrmHrEditorSnapshotTests.EditorInputs` | 25 public instance Snapshot-returning-own-type contracts in CrmHr.Contracts + one in Projects.Contracts; static helper excluded | 26 | 26 |
| `FloatingAgentChatSettingsValidatorTests.InvalidSettings` in `FloatingAgentChatArchitectureTests.cs` | Eight explicit yields containing FloatingAgentChatSettings | 1 | 8 |
| `MafFinalizerToolFactorySchemaCharacterizationTests.Contracts` | Eight explicit ExpectedBaselines yields containing AgentStructuredOutputContract | 1 | 8 |
| `ProcessCompatibilityToolPolicyTests.HistoricalPolicies` | 23 nonempty lines of HistoricalRows | 23 | 23 |
| `ProcessRuntimeCapabilityPolicyTests.LegacyDescriptorRows` | 88 nonempty lines of LegacyDescriptors | 88 | 88 |
| `ProcessRuntimeToolPreflightServiceTests.ProcessStartingToolCapabilityRoutes` | 21 initializer rows containing ProcessHostCapabilityId | 1 | 21 |
| `ProductToolPolicyTests.LegacyPolicyRows` | 107 nonempty lines of LegacyPolicies | 107 | 107 |
| `ProjectStructureAgentTransferFailureMapperTests.RejectionMappings` | Seven explicit initializer rows | 7 | 7 |
| `ProjectStructureAgentTransferFailureMapperTests.BatchDeletionRejectionMappings` | Two explicit initializer rows | 2 | 2 |
| `ProjectStructureAssetAgentFailureBoundaryTests.ExpectedAssetFailures` | Eight explicit yields | 8 | 8 |
| `ProviderUsageWindowTests.Selections` | Three selections × five ProviderUsagePeriod members | 15 | 15 |
| `SchedulerProjectionDecisionTests.Cases` | Three SchedulerPlanMisfirePolicy × six ScheduleCase members | 18 | 18 |
| `SecretScanningTests.RealisticSecretSamples` | Four SecretPatterns initializers; sample contents not copied into evidence | 4 | 4 |
| `StorageJsonTests.RootedOrPhysicalLogicalLocatorCases`, SerializeReference test | Seven literal locators × two locator kinds | 14 | 14 |
| Same provider, ParseReference test | Same independent fourteen rows | 14 | 14 |
| `WorkflowExternalResponseOperationTests.EveryTransition` | Nine WorkflowExternalResponseOperationState members squared | 81 | 81 |
| `WorkspaceImageOperationServiceTests.SupportedImages` | Three explicit byte-array initializer rows | 3 | 3 |
| **Total across 23 methods** | | **513** | **547** |

The reflective CRM/HR provider is bounded by the current source of the two
referenced Contracts assemblies, not by historical test totals. Its selected
types all expose the public zero-argument instance method with that same return
type. The Projects static collection snapshot helper has arguments and is not
selected.

## Deferred expansion evidence and final arithmetic

Only three methods require historical runner serialization evidence. The prior
same-method record is
`.artifacts/cd1-closure-20261011/stable-reconciliation-final.json`, Unit entry,
at the `f9dd2b02` checkpoint. It records these deferred expansions:

- `Process_starting_tool_contracts_have_deterministic_host_routes`: 1 → 21.
- `Validate_rejects_values_outside_inclusive_boundaries`: 1 → 8.
- `CreateCapture_produces_the_policy_tool_name_description_and_json_schema`: 1 → 8.

Source correspondence was checked independently with `git show f9dd2b02:<path>`:
the entire preflight and finalizer test files are unchanged, as are
ProcessHostCapabilities.cs and AgentOutputContracts.cs. The entire
FloatingAgentChatSettingsValidatorTests class and FloatingAgentChatSettings
record are unchanged even though their enclosing files have other edits. The
test project still uses xunit 2.9.3 and xunit.runner.visualstudio 3.1.4. No custom
xUnit serializer changes these values. Current source independently confirms
their 21/8/8 execution cardinalities. The prior aggregate count is not used as
the current count and no new runner list is used to derive the expectation.

```text
Discovery = 5,831 Facts + 3,907 InlineData + 513 MemberData = 10,251
Execution = 5,831 Facts + 3,907 InlineData + 547 MemberData = 10,285
Deferred expansion = (21 - 1) + (8 - 1) + (8 - 1) = 34
```

Repeated or truncated runner display labels remain separate rows; they must not
be deduplicated when comparing discovery output. A new list result different
from 10,251, or execution different from 10,285, requires source/identity
reconciliation rather than changing this expectation to match the runner.
