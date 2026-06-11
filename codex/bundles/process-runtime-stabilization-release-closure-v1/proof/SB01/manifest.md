# SB01 Proof Manifest

- Subbundle: `SB01`
- Status: `Completed`
- Owned requirements: `REQ-001`
- Raw notes: review real code and tests, determine current release posture, and stabilize process launching/execution before further Process Core extraction.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Bundle start SHA: `430496c5e7217a847e9172dcc0c2fba57f75f75c`

## Changed File Hashes

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs` | `40a38bb15f917c34bce49552e07203966a5d26c931323dbd8d1984a080b265a0` | `fc5ae9e6479b6f7fadd11bf0afb4539b40cb78c704b26db0ad150bda6490ea58` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/focused-test.txt`
- Source assertion transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`
- Code-first ratio transcript: `bundle://proof/SB01/transcripts/current-code-first-ratio.txt`
- Bundle-path coupling scan transcript: `bundle://proof/SB01/transcripts/bundle-path-coupling-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Semantic Adequacy

- Test name: `Process_runtime_host_codefirst_SB01_INV_009_ratio_report_rejects_conservative_head_fallback_unless_blocked`
- Test name: `Process_runtime_host_codefirst_SB01_INV_010_worktree_numstat_command_requires_explicit_start_sha`
- Invariant ID: `SB01_INV_009`
- Invariant ID: `SB01_INV_010`
- Shallow-pass trap: a release report can show green tests while using a conservative `HEAD` fallback or a commit-only diff that ignores uncommitted Codex changes.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-source-assertion.txt` exits non-zero against `HEAD` because `SB01_INV_009` and `SB01_INV_010` are absent there.
- Semantic positive proof: `bundle://proof/SB01/transcripts/focused-test.txt` exits 0 with 10 guard tests passing, including the fallback-policy and worktree-diff guards.
- Source assertion proof: `bundle://proof/SB01/transcripts/source-assertions.txt` cites the explicit SHA guard, worktree-inclusive diff guard, conservative fallback rejection, and representative automation path checks.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` exits 0 and reports no TODO or NotImplemented markers in the changed test file.
- Downstream smoke proof: `bundle://proof/SB01/transcripts/current-code-first-ratio.txt` records explicit start SHA `430496c5e7217a847e9172dcc0c2fba57f75f75c` and shows the interim ratio is not final closure proof.

## Closure Decision

- Entry gate: Passed after prepared-stage validation succeeded.
- Closure gate: Passed after focused guard tests, source assertions, ratio baseline capture, source scan, and anti-stub audit produced artifact-backed proof.
- Progression decision: SB02 may proceed; SB06 must rerun the final ratio from the explicit start SHA after implementation-heavy phases land.
