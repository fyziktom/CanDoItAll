# A06 handoff

## State

- A06 implementation and remediation: `Completed — Gate C3b/Hosting GO`
- Independent decision: initial `NO-GO` and bounded remediation `GO` in `reviews/19-a06-independent-review.md`
- A07: `Eligible — Gate C3b/Hosting GO`
- Checkout: branch `unix-adoption`, anchored at `27527039dd05299b3ed54ed2c3bc129cec2aeecf` plus the reviewed portability working tree

## Delivered

- Typed support manifest and strict artifact projection.
- Redacted runtime operations/readiness API with seven typed purpose-root facts.
- Four framework-dependent RID publishes.
- Direct local Components/FileTools project-reference development bridge with NuGet fallback.
- Unix immutable-release installer, launcher, rollback, systemd template, explicit-identity system LaunchDaemon template, and operator runbook.
- Durable Windows and Ubuntu actual-host provenance plus start/restart/upgrade/rollback evidence.
- Explicitly unverified macOS actual-host boundary.

## Authoritative evidence

- Primary report: `reviews/18-a06-evidence-report.md`
- Initial independent review/re-entry contract: `reviews/19-a06-independent-review.md`
- Machine summary: `artifacts/unix-portability/A06/final/A06-validation-summary.json`
- Full Unit: `artifacts/unix-portability/A06/final/windows/A06-windows-full-unit-authoritative.trx` — 5,561/5,561
- Focused: Windows 32/32 and native Ubuntu 32/32
- Publish: `artifacts/unix-portability/A06/final/publish/A06-publish-matrix.json` — four targets complete
- Actual host: Windows two healthy starts; Ubuntu v1/v2/rollback three healthy unprivileged starts with exact image/OS/runtime provenance
- Purpose roots: every Windows and Ubuntu operations capture contains seven Ready redacted facts
- Service identity: rendered systemd and launchd templates contain explicit service user/group with zero unresolved tokens
- Architecture: `snap-20260810044522-a1246e1e` — no Error finding or diagnostic
- Portability: 27,094/27,094 classified, 0 unclassified
- Redaction: schema-3 52 candidates/51 scanned plus one output control, 0 gaps, 2 sentinels/0 matches, six unique synthetic fixture fingerprints only

## Independent closure

The bounded re-review in `reviews/19-a06-independent-review.md` closed HOST-003 launchd identity, HOST-005 redacted per-purpose roots, and HOST-001 durable Windows/Ubuntu provenance. It also reconciled the refreshed build, test, CodeAnalytics, portability, and redaction evidence package.

## Residual work

- `A07-MACOS-HEADLESS-ACTUALHOST-001`: run the published macOS profile on a genuine x64 or arm64 Mac and keep the manifest unverified until it passes.
- `MACOS-KEYCHAIN-VALIDATION-001`: later genuine Keychain validation authorized by the operator as non-blocking for current progression.
- A07 owns the active three-platform CI/integration/restart gate and final C4 closure, subject to the explicit real-Mac deferrals.
