# 01 Foundation Baseline and Guardrails

## Status

- `Completed`

## Objective

- Establish the shared test infrastructure, environment fixtures, and anti-fake proof guardrails that every later subbundle will rely on.
- Convert the current single-DB test harnesses into reusable multi-profile test foundations before feature code starts spreading across the repo.

## Covered Inputs

- `RQ-019` unit coverage expansion
- `RQ-020` integration coverage expansion
- `RQ-022` browser-proof planning support
- `RQ-023` no fake validation
- Raw notes `N-08`, `N-09`, `N-10`

## Prerequisites

- none
- The prepared bundle validator must pass before implementation starts.

## Exact Source References

- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkbenchStateServiceTests.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/TestApplication.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`
- `C:\repositories\CanDoItAll/docker-compose.yml`
- `C:\repositories\CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`

## Deliverables

- Shared test support for creating temporary control-plane roots, managed SQLite profiles, and seeded profile-scoped storage roots.
- Shared PostgreSQL test-availability helper(s) that either provision local Docker-backed PostgreSQL or skip/block honestly with a clear message.
- Shared fake IPFS transport test host/server for transport-layer automated tests.
- Shared seed helpers that can create visibly different data in at least two profiles and at least one managed file per profile.
- Updated execution/test documentation or helper conventions so later subbundles can log proof consistently.

## Dependency Impact

- Every later subbundle depends on these fixtures to prove its work without rebuilding one-off harness logic.
- Weak proof or missing fixtures here would encourage fake or partial validation in subbundles 02–08.
- PostgreSQL and IPFS proof in subbundle 08 is not credible unless the support utilities introduced here are real and reusable.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add shared test-support classes/helpers that can build temporary control-plane roots, multiple database profiles, and seeded workspace roots without duplicating setup code across unit, integration, component, and Playwright projects.
2. Refactor the existing integration/component/browser harnesses so they can consume an active-profile-aware bootstrap path instead of hardcoded one-DB SQLite startup assumptions.
3. Add a PostgreSQL availability helper that uses repo-local Docker defaults when available and reports a blocked/skip condition clearly when PostgreSQL is unavailable.
4. Add a fake IPFS HTTP test server/helper so later snapshot transport tests can run deterministically without needing a real node.
5. Add seed helpers that write both database data and managed files so later clone/switch/storage tests have unambiguous isolation proof.
6. Add or update unit tests around the new test-support primitives themselves where failure modes are non-trivial.

## Scope Exceptions

- This subbundle does **not** implement database profile business logic yet.
- This subbundle does **not** expose any end-user UI yet.
- This subbundle may leave real PostgreSQL/browser execution for later subbundles, but the support plumbing must exist now.

## Do Not Do

- Do not implement runtime switching logic in this phase.
- Do not add one-off ad hoc harnesses that later subbundles cannot reuse.
- Do not mark PostgreSQL or IPFS proof complete in this phase; only the support infrastructure belongs here.

## Acceptance Checklist

- Shared test-support utilities can create at least two distinct profiles with isolated data roots.
- The integration harness can bootstrap from a profile descriptor instead of only from hardcoded SQLite configuration.
- PostgreSQL-dependent tests have a single honest availability gate instead of hand-written conditional logic per test.
- A fake IPFS server/helper exists for later automated transport tests.
- Later subbundles can reference these fixtures instead of duplicating setup logic.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Database|FullyQualifiedName~Workbench|FullyQualifiedName~Profile|FullyQualifiedName~Snapshot"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Database|FullyQualifiedName~Profile|FullyQualifiedName~Harness"`
- Capture the names of the newly added support tests and the command output summary in `reviews/01-execution-report.md`.
- If PostgreSQL or IPFS helper validation is blocked by environment, record that as `Blocked`, not `Completed`.

## Browser Validation Logging

- `N/A` — this subbundle does not yet change browser-visible product UI.
- Record `N/A` explicitly in the browser analytics section for this subbundle and reference the later subbundles that will consume the fixtures.
- If harness-level browser setup changes are made, log them under commands rather than pretending they are end-user browser proof.

## Progression Gate

- Shared fixtures for multi-profile setup, PostgreSQL availability, and fake IPFS transport must exist and be referenced by downstream tests.
- The execution report must show the support-test commands and outcomes before subbundle 02 starts.

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Focus on shared test foundations and anti-fake guardrails:
- reusable multi-profile test setup
- PostgreSQL availability helpers
- fake IPFS transport helpers
- seeded profile data/files
- harness refactors needed by later phases

Do not implement runtime switching or UI yet.
Run the required tests, record the evidence in the execution report, and stop if proof is blocked.
```
