# SB021 Proof Manifest

## Status
Completed.

## Objective
Gate G: live smoke passed or explicitly skipped.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 live-provider policy subset.
- Critical invariant contract: `bundle://proof/SB021/semantic-invariants.md`
- Downstream dependency: SB022-SB024 may start because Gate G has an explicit policy decision; it must not treat this as live-provider functionality passing.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `7c73ee57b1f64f6e26dd3de84cd2cbedb51a635faa65ad49c3ee223f81bf29e6` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB021/README.md` | `b23b51a0847464d26ed73f6b6d5680f64a261e661f987ee938f93884c2b4c88d` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB021/transcripts/live-openai-gate-decision.txt` | `7260da8d672b4b1b92bad360dfe733f0ecff0c06ff69a3ae84feec15f8a5bcc2` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB021/transcripts/live-openai-gate-source-assertions.txt` | `21432f71e7355818b6b953eddd64a88591ff8f9b432c2725438cb0fdccac678b` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB021/red-team/deterministic-tests-not-live-proof.txt` | `58ed3ddd446744dec1aec4e3932e6c490e010136f370a2dfb10e80ac89538708` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/architecture/02-openai-live-smoke-policy.md` | `38615627a8ff3f43d3c2bce9120c9560c491fdeb86dadfe1b29886c663c20753` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/templates/openai-live-smoke-proof-template.md` | `39b7b0b86e9e0fe374f8eb2a902aeb602e90b5e5df5723792cbab8c18150e243` |

## Command Transcripts
- Gate decision: `bundle://proof/SB021/transcripts/live-openai-gate-decision.txt`
- Source/policy assertions: `bundle://proof/SB021/transcripts/live-openai-gate-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB021/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB021/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team deterministic-not-live rejection: `bundle://proof/SB021/red-team/deterministic-tests-not-live-proof.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Live smoke policy decision | `architecture/02-openai-live-smoke-policy.md` and configuration transcript | Execution report and downstream gates | Opt-in flag, key, budget, and timeout must all be present before live call | Red-team proof rejects counting deterministic tests as live OpenAI proof |
| Redacted configuration evidence | `live-openai-gate-decision.txt` | Gate G review | Captures presence/absence only, never secret values | API key value is not printed |
| Live smoke proof template | `templates/openai-live-smoke-proof-template.md` | Future opted-in live run | Defines request/response hashes, token metadata, duration, and artifact IDs | Current run has N/A values because live smoke was skipped |

## Closure
- Shallow-pass trap: A fake pass could count deterministic mock-provider tests as live OpenAI proof or run a live call without opt-in/budget.
- Adversarial negative proof: `bundle://proof/SB021/red-team/deterministic-tests-not-live-proof.txt`
- Semantic positive proof: explicit config/opt-in skip in `bundle://proof/SB021/transcripts/live-openai-gate-decision.txt`
- Anti-stub audit: `bundle://proof/SB021/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Gate G is explicitly skipped by policy; this is not a live-provider functionality pass.
