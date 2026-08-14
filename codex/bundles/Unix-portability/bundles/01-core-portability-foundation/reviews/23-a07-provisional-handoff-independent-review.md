# A07 provisional handoff independent review

Date: 2026-08-10

## Decision

**GO for the operator-authorized provisional implementation handoff.** No blocker was found in the bounded A07/C4 exception, its core/runtime status reconciliation, or its validator guard.

This decision is not C4 GO, macOS validation, hosted validation, merge readiness, or a support/release claim. It permits the runtime bundle to enter B00 against the exact recorded anchors and to continue implementation through its normal internal gates. Core C4 and runtime R4 remain deferred and must still receive their originally required hosted, actual-host, policy, artifact, and independent-review evidence before release closure.

## Findings

No blocking finding remains.

### Claim integrity

- `reviews/22-a07-hosted-validation-deferral.md` identifies the operator decision, tracking id, exact anchors, permitted progression, deferred evidence, non-waived quality rules, and re-entry procedure. It explicitly says C4 is `DEFERRED`, not `GO`.
- `reviews/CORE-C4-HANDOFF.md` is labeled `Provisional implementation handoff approved — C4 remains deferred`; its gate result is `PROVISIONAL IMPLEMENTATION HANDOFF — C4 DEFERRED`.
- The macOS profile remains `ActualHostUnverified`. General macOS build/test/PostgreSQL migration/publish/headless execution and the separate Keychain proof are listed as deferred. No hosted or genuine-macOS pass was inferred from cross-publish, injected-native contracts, Windows, or Linux evidence.
- The A07 task and exit surfaces preserve incomplete actual-host/C4 items as unchecked. The C4 gate-log row says `DEFERRED`, and the core execution report says the handoff is satisfied for implementation progression only.
- Historical review 21 correctly remains an earlier local-readiness decision that blocked B00 at that time. Review 22 is an explicit later operator exception; the historical text was not rewritten into a false C4 decision.

### Anchor verification

The three recorded commits exist and are the current exact heads of the named remote branches. GitHub comparisons returned `identical`, zero commits ahead, and zero commits behind:

| Repository | Branch | Recorded and remote head |
|---|---|---|
| `fyziktom/CanDoItAll` | `unix-adoption` | `dd78ffa9769ba1d125b8be81a4b303df37c32505` |
| `fyziktom/CanDoItAll.Components` | `development` | `8372c1d55f21b349f8e859470b02eeb4421e96ca` |
| `fyziktom/CanDoItAll.FileTools` | `development` | `f31e20d054003348c7557b9634e0838fc5996ae0` |

The local branches and remote-tracking references agree with those SHAs. Components and FileTools are currently clean. CanDoItAll is intentionally dirty only with the provisional bundle/status/validator records created after the pushed anchor; the anchor-to-worktree review found no product or non-bundle source delta.

### Runtime entry boundary

- The runtime root, manifest, requirement register, phase plan, inventories, reports, and B00 files consistently accept either C4 or the explicit provisional handoff.
- The exception opens B00 for re-anchoring, ownership review, characterization, inventory, and subsequent implementation progression. It does not mark B00/R0 complete: R0 remains not started, B01 remains blocked by R0, downstream subbundles remain blocked by their named gates, and R4 remains not started.
- Updated `RPREP-001` requires immutable core/sibling anchors, the operator exception, local evidence, deferred support proof, current delta/dirty state, and invalidated source references. B00-T01 must stop if the provisional handoff is incomplete or overclaims support.
- Final runtime closure still requires C4 to be valid. The exception therefore narrows the authorization to implementation and local validation rather than silently weakening the release gate.

### Validator behavior

- Positive portable validation with the provisional record present passed: 305 files, zero errors, zero warnings, using `--skip-checksums` while the final review/index freeze is pending.
- In an isolated temporary copy, removing only `reviews/22-a07-hosted-validation-deferral.md` caused the runtime validator to emit exactly one warning on the runtime manifest: the runtime bundle must remain blocked until C4 or an explicit provisional handoff is accepted. The temporary copy was removed after the check.
- The validator also requires both runtime manifest status and entry-gate text to identify the provisional path; a record file alone does not activate it.

The missing-record guard is a warning and therefore exits successfully. That satisfies the requested “fails/warns” behavior, but automation must not discard validator warnings. Promoting this specific guard to an error would be appropriate if future unattended CI is expected to enforce authorization fail-closed.

## Residuals and required bookkeeping

- Hosted Windows/Ubuntu/macOS jobs, genuine macOS execution, hosted artifact download/checksum/redaction, repository required-check policy, real C4, and final R4 remain mandatory re-entry work under `HOSTED-PORTABILITY-VALIDATION-001`.
- `MACOS-KEYCHAIN-VALIDATION-001` remains separately deferred and must stay `ActualHostUnverified` until genuine Keychain execution passes.
- Workflow action SHA pinning and graceful Windows service-stop proof remain the previously recorded non-blocking hardening/operations follow-ups.
- After this review is added, regenerate `bundle-index.json` and `CHECKSUMS.sha256`, then run the portable validator without `--skip-checksums`. Do not rewrite the historical exception when later C4/R4 evidence is collected; add the final decisions and link back to it.

## Scope confirmation

No full test suite was rerun. The review used only read-only source/status inspection, remote anchor comparisons, the portable validator, and an isolated negative validator copy. No product source, canonical status file, evidence artifact, index, or checksum was edited by this reviewer; only this independent review file was added.
