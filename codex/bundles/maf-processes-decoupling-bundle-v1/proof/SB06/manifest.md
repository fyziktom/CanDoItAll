# SB06 Proof Manifest

## Subbundle

- ID: SB06
- Title: Process tool parity and policy regression suite
- Status: Completed
- Critical foundation: Yes
- Owned requirements: RQ-005, RQ-006, RQ-007, RQ-008, RQ-009, RQ-010, RQ-011, RQ-014
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeToolProviderArchitectureTests.cs` | `A95437E512B471752EC93385284EFB6F255C509451AFFB96D9B80CC7ADC975EA` | Strengthens architecture guard to assert MAF no longer references Processes directly. |
| `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` | `4A98F1C40A3EBE4CEDAC7EB21579147B564F8237ADB17F43A17B06D76B79D869` | Adds exact 23-process-tool catalog and capability-registry regression. |
| `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | `1C15266B7B99327C48AF1034813DD6CE5BBB28B3217B69113E54000AF1F50732` | Strengthens zero-provider MAF behavior test to prove no process tools attach without registered providers. |
| Changed file hash transcript | `bundle://proof/SB06/source-assertions/changed-file-hashes.txt` | Full hash evidence for touched SB06 test files. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "AgentRuntimeToolProvider"` | `bundle://proof/SB06/transcripts/agent-runtime-tool-provider-tests.txt` | 0 | Proves provider architecture/composition guardrails. |
| `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "AgentToolInvocationPolicy"` | `bundle://proof/SB06/transcripts/agent-tool-invocation-policy-tests.txt` | 0 | Proves policy, catalog, and capability-registry regressions. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "AgentFrameworkExecutionCapabilityFiltering"` | `bundle://proof/SB06/transcripts/agent-framework-execution-capability-filtering-tests.txt` | 0 | Proves required execution capability filtering integration slice. |
| `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "ProcessAgentRuntimeToolProvider"` | `bundle://proof/SB06/transcripts/process-agent-runtime-tool-provider-tests.txt` | 0 | Proves registered Processes provider parity and access behavior after SB06 edits. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB06/transcripts/solution-build.txt` | 0 | Proves full solution builds after regression test changes. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production preserved failing-first transcript; policy, capability, zero-provider, and provider parity tests are the maintained regression proof.
- Passing transcript: `bundle://proof/SB06/transcripts/agent-tool-invocation-policy-tests.txt`.
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| Exact process tool regression source assertion passed. | `bundle://proof/SB06/source-assertions/process-tool-regression-source-assertion.txt` | Inventory count 23; integration exact-name test contains all 23; policy test contains 23 process metadata constants. |
| SB06 guard tests exist in source. | `bundle://proof/SB06/source-assertions/sb06-test-source-audit.txt` | Architecture, zero-provider, and catalog/registry regression tests are present. |
| Anti-stub audit passed. | `bundle://proof/SB06/source-assertions/anti-stub-audit.txt` | No TODO, `NotImplemented`, stub, or placeholder matches in SB06 changed tests. |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | Hardens the small-step decoupling so process tool behavior cannot be silently simplified, omitted, or re-coupled. |
| Shipped behavior | Regression suite now guards direct architecture, zero-provider MAF behavior, exact process tool inventory, read/mutation policy, and provider registration behavior. |
| Source proof | `bundle://proof/SB06/source-assertions/process-tool-regression-source-assertion.txt` and `sb06-test-source-audit.txt`. |
| Test proof | `bundle://proof/SB06/transcripts/agent-runtime-tool-provider-tests.txt`, `bundle://proof/SB06/transcripts/agent-tool-invocation-policy-tests.txt`, `bundle://proof/SB06/transcripts/agent-framework-execution-capability-filtering-tests.txt`, `bundle://proof/SB06/transcripts/process-agent-runtime-tool-provider-tests.txt`, and `bundle://proof/SB06/transcripts/solution-build.txt`. |
| Shallow-pass trap | Count-only parity would miss names; registry-only parity would miss runtime provider behavior; runtime-only parity would miss policy catalog drift. SB06 tests cover all three. |
| Adversarial negative proof | A missing known tool, missing capability-registry entry, direct MAF Processes project reference, or zero-provider process tool leak would fail targeted tests. |
| Semantic positive proof | All 23 tools remain exact-name tested; read tools remain approval-free; mutation tools require approval; registered Processes provider still attaches tools. |
| Anti-stub audit | `bundle://proof/SB06/source-assertions/anti-stub-audit.txt`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB06 adds regression tests only; it introduces no persisted production state, signal, record, or event. | N/A | N/A | N/A |
