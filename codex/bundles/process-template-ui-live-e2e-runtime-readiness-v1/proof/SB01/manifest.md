# SB01 Proof Manifest

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: `REQ-001`
- Raw notes: review real code and tests, preserve code-first closure, and prevent proof-only bundle drift.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Hashes

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | `a06c6918f5dcceb0d37b1e3b254e7a4674308b72095bd76d778deb875352ae65` | `a7df1cb1293a4f7a952045c805677a01f96526ba630fd4aae78415634a56702e` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/focused-test.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Bundle-path coupling scan transcript: `bundle://proof/SB01/transcripts/bundle-path-coupling-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Semantic Adequacy

- Test name: `Process_runtime_host_codefirst_SB01_INV_006_numstat_command_requires_explicit_current_bundle_start_sha`
- Test name: `Process_runtime_host_codefirst_SB01_INV_007_long_running_template_e2e_proof_cites_production_dispatch_path`
- Invariant ID: `SB01_INV_006`
- Invariant ID: `SB01_INV_007`
- Shallow-pass trap: A stale or branch-name ratio baseline and manual-transition E2E proof can both look green without proving this bundle's source/test-heavy closure.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` exits non-zero against `HEAD` because the new SB01 invariants are absent there.
- Semantic positive proof: `bundle://proof/SB01/transcripts/focused-test.txt` exits 0 with all seven guard tests passing.
- Source assertion proof: `bundle://proof/SB01/transcripts/source-assertions.txt` cites the explicit SHA guard, production automation path tokens, `SB01_INV_006`, `SB01_INV_007`, and `ProcessDryRunExecutionPipeline`.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` exits 0 and reports no TODO, NotImplemented, or `throw new NotImplementedException` markers in the changed test file.
- Downstream smoke proof: `bundle://proof/SB01/transcripts/bundle-path-coupling-scan.txt` exits 0 and limits bundle-path coupling to intentional guard fixtures.

## Closure Decision

- Entry gate: Passed after prepared-stage validation succeeded.
- Closure gate: Passed after focused guard tests, source assertions, source scan, and anti-stub audit all produced artifact-backed proof.
- Progression decision: SB02 may proceed; final SB08 ratio must use an explicit current bundle start SHA.

