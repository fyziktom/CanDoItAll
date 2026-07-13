# SB03 Proof Manifest

Status: `Completed`

Owned requirements: `RQ-006`, `RQ-007`

Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Failing-first build after package update | `bundle://proof/SB03/transcripts/build-failing-first.md` |
| Build after `UseScriptApproval` compatibility fix | `bundle://proof/SB03/transcripts/build-after-skill-approval-fix.md` |
| MAF composition tests after new assertions | `bundle://proof/SB03/transcripts/maf-composition-tests.md` |
| Focused MAF/process unit slice | `bundle://proof/SB03/transcripts/focused-unit-tests.md` |
| Full Release build after tests | `bundle://proof/SB03/transcripts/build-after-tests.md` |
| Source assertions | `bundle://proof/SB03/transcripts/source-assertions.md` |
| Anti-stub audit | `bundle://proof/SB03/transcripts/anti-stub.md` |
| Changed file hashes | `bundle://proof/SB03/transcripts/changed-file-hashes.md` |

## Changed-File Manifest

| File | After SHA-256 |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs` | `AEAC20344D6C4132B2F62B2C172601F1AA1F630E3D70F98D008077AB085342EF` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | `B7F9C6E039088D5E6A8C97CF78FCB3CF1B71131EA43CF88A3314D1AF342A53F8` |

## Compatibility Finding

MAF 1.13 removed `AgentSkillsProviderBuilder.UseScriptApproval(bool)` and replaced approval configuration with `AgentSkillsProviderBuilder.UseOptions(Action<AgentSkillsProviderOptions>)`.

The compatibility fix preserves CanDoItAll behavior by disabling approval for read-only `load_skill` and `read_skill_resource` operations, while leaving `run_skill_script` approval enabled only when the selected skill capability explicitly requires script approval and approval requirements are not suppressed.

## Source Assertions

- `UseScriptApproval` no longer appears in C# source.
- `ProcessAgentRuntimeToolProvider` was not introduced in C# source.
- No `/api/processes/definitions` or `processes/definitions` route expansion was introduced.
- Targeted MAF project files no longer contain stable `Microsoft.Agents.AI` `1.8.0` references.
- The production compatibility path uses the MAF 1.13 `DisableLoadSkillApproval`, `DisableReadSkillResourceApproval`, and `DisableRunSkillScriptApproval` options.

## Test Summary

- `MafAgentRuntimeToolProviderCompositionTests`: 35 passed.
- Focused MAF/process unit slice: 330 passed.
- Full Release build: passed with only the pre-existing `Microsoft.OpenApi` NU1903 advisory.

## Production Behavior Artifact Matrix

No new production signal, state record, external route, process tool provider, or workflow surface was introduced in `SB03`.
