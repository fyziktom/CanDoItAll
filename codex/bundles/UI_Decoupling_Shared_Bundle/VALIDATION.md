# Impact-based validation and evidence

Current `docs/testing.md`, current CI and repository portability rules govern command details. This companion makes the selection process explicit; it does not shrink mandatory proof.

## Test selection is a union, not a magic oracle

Use the configured CanDoItAll CodeAnalytics MCP first for changed symbols/files, references, callers, owning tests and affected consumers. Discover its actual tool schema; do not invent an API name. Record index revision/freshness and sanitized query/results. Reindex or compensate for dirty/new source before relying on it.

Select the union of:

1. MCP-identified directly and transitively affected tests.
2. Actual callers/contracts, partial test classes, shared DI/composition, serialization/reflection and routes.
3. Razor descendants, deferred/nested overlays, event bindings, JS/CSS/Tailwind/static assets and sandbox registrations.
4. The child's mandatory feature/security/origin/receipt regression floor, including newly added tests.
5. Specific fixes and neighboring user journeys whose behavior could change.

CodeAnalytics can miss new/untracked files, generated bindings and dynamic composition. Its absence is not permission to skip tests: record it and use bounded source/call-site/configuration search plus the mandatory floor. Do not claim certainty from either a text scan or an index. A changed contract or newly discovered consumer refreshes the plan.

## Inner loop

Build every changed production project directly using an isolated configuration. Select the narrowest owning test method/topic, not the entire solution. Derive the expected data-expanded case count from current source, then run build-backed `--list-tests` with the exact intended filter. Investigate zero or unexpected discovery. Record both discovery and execution expansion when dynamic data differs.

Execute exactly the confirmed filter and configuration. `--no-build --no-restore` is valid only after that test assembly and dependencies were refreshed from the current source. Use separate output/configuration for concurrent lanes and set `CANDOITALL_TEST_CONFIGURATION` consistently for browser child hosts when applicable.

Preserve historical test obligations when refactoring private-member tests. Replace implementation-coupled access with the actual new owner/contract where appropriate; retain the behavior and theory rows. Do not simply delete assertions, rename a class out of the filter, skip tests or lower discovery expectations to obtain green results. Current bUnit guidance requires awaited `ClickAsync`, dispatcher-safe Find+event operations, actual editor readiness, and the repository's `DisposeRenderedComponentsAsync` helper.

## Proof layers

| Layer | What it proves | What it does not prove |
| --- | --- | --- |
| Light unit/component | Draft, projection, read/operation fencing and actual renderer behavior | Persistence or native authorization |
| Native owner/integration | Real commit/refusal, exact identities, scope, read-back and API compatibility | Browser layout/JS or a real external model |
| Independent browser | Actual full renderer, scenario transitions, JS/assets/focus/dialogs | Production owner wiring |
| Production browser | Actual routes, controls, native integration and observable effect | External model success when its upstream is scripted |
| Source/published graph and assets | Delivered dependencies/assets and clean startup | Behavior never exercised |
| Live/paid/native hardware | Only the explicitly executed external/hardware behavior | Any closed-gate/rehearsal path reported as passed by a runner |

Use deterministic external-provider fixtures with the real application/owner where possible. Do not call them live-model tests. Current testing guidance warns that a runner can report a closed live gate or rehearsal as passed: inspect the evidence manifest, actual execution field and counted provider requests. No old budget is renewed.

## Broad Stable and final invalidation

Do not run an unfiltered project or Stable per stage. At a named final frozen checkpoint assess current documented triggers: shared/public contracts, source/build graph, common DI/assets, native persistence/authority, HTTP compatibility, or an explicit release/merge closure. A complete Processes render extraction is expected to change contracts/composition and therefore to warrant one final broad Stable run after focused lanes are stable. Record the actual trigger; a demonstrably local later edit needs only its affected rerun unless it invalidates broader evidence.

Record exact command, trait filter, expected/actual counts, skipped/quarantined cases, result and candidate. Broad Stable is not the Playwright/live-process/Docker/runtime-portability lane. A failed, unavailable or unrun required lane is not green. Do not repeat an entire historical demo/customer campaign without a current scope reason.

## Mandatory portability and documentation

For changes in protected scope, run current portability helper self-tests, secret-artifact checks, scan and enforcement. Typical repository entry points reviewed in v3/current guidance are:

```text
python tools/Validation/Portability/test_enforce_portability_baseline.py
python tools/Validation/Portability/test_scan_artifacts_for_secrets.py
python tools/Validation/Portability/scan_portability.py --repo-root . --output <owned-scan.json> --tracked-only
python tools/Validation/Portability/enforce_portability_baseline.py --scan <owned-scan.json> --baseline tools/Validation/Portability/portability-risk-baseline.json
```

Read their current CLI and `docs/testing.md` first. A tracked-only scan does not cover new untracked source; include it by the supported full scan or reviewed staging workflow. Repair actual defects. For intentional ADDED/STALE findings, use the documented reviewed-baseline procedure, inspect the baseline diff, and rerun final enforcement without `--write-baseline`. Never waive a failing gate as “CI-only”. Run the current documentation/link validation entry point when changing maintained docs.

## Evidence record

For each acceptance row retain: source/configuration candidate, test/filter or actual manual journey, expected versus discovered/executed count where applicable, real oracle, outcome, sanitized artifact location and any limitation. The statuses are `NOT_RUN`, `PASS`, `FAIL`, `BLOCKED`, `NOT_APPLICABLE` and `STALE`. NOT_APPLICABLE requires an explicit scope reason and cannot erase a mandatory row. PASS requires actual execution and the relevant effect oracle, not a copied earlier receipt.

The package templates intentionally start NOT_RUN. They are not a predetermined list of passing test counts. Keep actual results outside sealed inputs; update maintained architecture/testing records with precise provenance and remaining gaps. Package helper tests validate only the helper, never the C# application.
