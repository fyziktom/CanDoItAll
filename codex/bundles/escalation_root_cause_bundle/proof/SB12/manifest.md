# SB12 Proof Manifest

## Status

- Result: Completed
- Date: 2026-07-08
- Scope: final validation for SB01 through SB11 escalation repair implementation.

## Validation Evidence

| Evidence | Result | Transcript |
| --- | --- | --- |
| Focused unit sweep for launch variables, completion gates, recovery, dispatch, operator, engine, tool preflight, launch executor, template compatibility, and runtime-owned .NET setup executor | Passed: 237 passed, 0 failed | `transcripts/01-focused-unit-tests.txt` |
| Strict full-pack template validation | Passed: 2 passed, 0 failed | `transcripts/02-template-validation.txt` |
| Integration launch/runtime boundary checks | Passed: 4 passed, 0 failed | `transcripts/03-integration-tests.txt` |
| Incident-equivalent regression for safe retry, duplicate fingerprint escalation, and budget-exhausted packet | Passed: 5 passed, 0 failed | `transcripts/04-equivalent-incident-regression.txt` |
| Manual 5032 equivalent note | Live 5032 not mutated; equivalent local reproduction used | `transcripts/05-manual-5032-equivalent-note.txt` |
| Solution build | Passed: 0 errors, 16 known NU1903 warnings | `transcripts/06-solution-build.txt` |
| CodeAnalytics dependency refresh | Passed: snapshot `snap-20260708214607-6650a5f9`, cycles `[]` | `transcripts/07-codeanalytics.txt` |
| Anti-stub audit | Passed: no matches in SB11/SB12 executor, adapter, DI, and test files | `transcripts/08-anti-stub-audit.txt` |
| Source assertions | Passed: executor, DI registration, adapter hook, idempotent receipt, and regression tests present | `transcripts/09-source-assertions.txt` |
| Completed bundle validator | Passed: completed-stage validator succeeded | `transcripts/10-completed-bundle-validation.txt` |

## Known Non-Blocking Findings

- `NU1903` for `Microsoft.OpenApi` 2.0.0 remains a pre-existing advisory warning in the broader solution graph. SB12 ran with `WarningsNotAsErrors=NU1903` and recorded it explicitly.
- CodeAnalytics service-registration search did not surface the `TryAddScoped<IDotNetSolutionSetupRuntimeExecutor, DotNetSolutionSetupRuntimeExecutor>()` registration. Source assertion transcript proves the registration exists in `ProcessesModuleServiceCollectionExtensions.cs`.

## Closure

SB12 closes the final validation gate. The incident class is covered by equivalent local reproduction instead of mutating the live blocked 5032 instance.

## Completed Validator Metadata

- Semantic invariant contract: `proof/SB12/semantic-invariants.md`.
- Portable source proof: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`.
- Portable bundle proof: `bundle://proof/SB12/changed-file-hashes.txt`.
- SHA-256 changed-file hash: `64133885D23079F279CFB46F64F74E6F022E90EE61C34E73805481F0E2907C78`.
- Passing transcript: `proof/SB12/transcripts/01-focused-unit-tests.txt`.
- Anti-stub audit transcript: `proof/SB12/transcripts/08-anti-stub-audit.txt`.
- Failing-first: N/A - process/non-production final proof uses adversarial negative tests inside the passing targeted transcripts and preserves live 5032 evidence by not mutating the blocked instance.


