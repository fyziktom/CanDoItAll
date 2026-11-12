# Broad Stable: monolithic Components source expectation

Frozen source accounting on 2026-10-16 for
`tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`.
Expected **3,090 discovered cases and 3,090 runtime cases**, before runner
enumeration. This is an expectation, not execution evidence.

The exact canonical `docs/testing.md` Broad Stable filter is:

```text
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

Source attribute inspection and independent Roslyn syntax counts agree:

| Source contribution | Methods | Expanded cases |
| --- | ---: | ---: |
| Fact | 1,767 | 1,767 |
| Theory with InlineData | 363 | 1,176 |
| Theory with MemberData | 8 | 147 |
| Total | 2,138 | 3,090 |

There are 383 local C# source files, including helpers and partial files. The
project explicitly links `../CanDoItAll.AgentFramework.UI.Tests/ProviderHistoryUiFixture.cs`;
it contains no attributed tests. All 57 source Trait attributes are
`Category=HostPlatform`, which this filter retains. There are no additional
canonical exclusions, custom Fact/Theory/data attributes, ClassData, test Skip
options or disabled discovery enumeration. No conditional test blocks were
found. Inline rows use serializable primitive/enum/Type inputs.

## MemberData cardinality and identity

Namespace prefixes below are relative to `CanDoItAll.Tests.Components`.

| Test class / provider | Static derivation | Cases |
| --- | --- | ---: |
| `AgentFramework.AgentsUsageOwnershipTests.Retirements` | `AgentsOverviewDetail` 3 × `UsageOwnerRetirement` 3 × Boolean 2 | 18 |
| `CrmHr.CrmHrUiModuleBoundaryTests.Workspaces` | Seven literal `TheoryData<Type,Type,Type>` entries | 7 |
| `Processes.ProcessWorkspaceShellTests.AuthoringRetirements` | Five `AuthoringFamily` × three `AuthoringRetirement` × Boolean 2 | 30 |
| `Processes.ProcessWorkspaceShellTests.AuthoringReadOrders`, receipt/read-order test | Five families × Boolean 2 | 10 |
| `Processes.ProcessWorkspaceShellTests.AuthoringReadOrders`, same-ID successor test | Same provider, independently used by a second theory | 10 |
| `ProjectStructure.ProjectStructureTaskQuoteLifetimeTests.RetiredQuotes` | Twelve `Retirement` × three `Completion` | 36 |
| `AgentFramework.WorkflowOwnershipTests.ReadLifetimes` | Four `DelayedReadLane` × three `DelayedReadOutcome` × Boolean 2 | 24 |
| `AgentFramework.WorkflowSurfaceTests.Scenarios` | Twelve `WorkflowScenario` values | 12 |
| Total | | 147 |

The external enum definitions are
`src/UI/CanDoItAll.AgentFramework.UI/Overview/AgentsOverviewState.cs` and
`src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/WorkflowSandboxFixture.cs`.
Other enumerations and literal provider bodies are in the named test source
files; the Processes methods share `ProcessWorkspaceShellTests.Drafts.cs`.
No provider was invoked. These rows contain only enums, Boolean values and
`System.Type`, so the configured xUnit/VSTest pre-enumeration should expose each
row during discovery; no complex descriptor forces a deferred single-theory
discovery count.

## Inheritance and independent cross-check

The independent source artifact is
`.artifacts/concurrent-mutations/validation/stable-source-attributes.json`,
entry `Project=CanDoItAll.Tests.Components`. Its `InterestingTypes` were reviewed:
the seven concrete test classes inheriting `AgentMemorySettingsPanelTestBase`
gain no tests from that helper, which has no Fact/Theory methods. `BunitContext`
and `IDisposable` bases add none. The other abstract bridge and generic logger/
read helpers have no test attributes. There is no generic/abstract attributed
test class requiring multiplication or exclusion. Partial declarations are
counted by actual attributed methods, not filenames.

No historical result was used as the numerical baseline. No build, discovery,
test/provider execution, database access or portability scan was performed by
this accounting task. Fresh parent-owned discovery must confirm the expectation;
a mismatch requires source/filter/serialization reconciliation before accepting
runtime proof. Recount if attributes, provider inputs, compile inclusion or the
canonical filter change.
