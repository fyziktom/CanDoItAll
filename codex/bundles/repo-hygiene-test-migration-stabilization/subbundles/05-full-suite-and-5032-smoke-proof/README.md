# 05-full-suite-and-5032-smoke-proof

## Status

- `Completed`

## Objective

Close the hygiene bundle with build/test proof and a fresh `5032` runtime smoke test for manual user validation.

## Covered Inputs

- RH-009: rebuild and start `localhost:5032`.
- RH-010: distinguish fixed failures from any remaining unrelated suite failures.

## Prerequisites

- SB01 through SB04 are complete or have explicit blockers recorded.
- Latest build output is not locked by stale `CanDoItAll.Web`/`dotnet test` processes.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Build transcript.
- Targeted repaired test transcript.
- Full unit-suite transcript or exact remaining unrelated failures.
- Fresh `5032` app process started from the rebuilt workspace.
- HTTP/browser smoke proof for `localhost:5032`.

## Dependency Impact

- This is the final closure and user handoff proof.

## Validation Depth

- End-to-end regression and runtime smoke closure.

## Implementation Steps

1. Ensure no stale app/test runner is locking build output.
2. Run `dotnet build` for the affected app/test surface.
3. Run targeted repaired test filters.
4. Run the full unit suite with hang/blame timeout or a bounded transcript strategy.
5. Rebuild/start the app on `5032`.
6. Smoke-test `localhost:5032` with HTTP and browser proof.
7. Update `reviews/01-execution-report.md` and browser analytics.

## Scope Exceptions

- If full suite still has unrelated failures, record exact failing tests and block only if they overlap this bundle's repaired surfaces.

## Do Not Do

- Do not serve a stale already-running process as proof.
- Do not skip browser/API smoke after a successful build.
- Do not mark full-suite proof clean if it was interrupted or hung without a blame artifact.

## Acceptance Checklist

- [x] Build succeeds with known warnings documented.
- [x] Targeted tests pass.
- [x] Full unit-suite outcome is recorded.
- [x] `5032` responds from a fresh process.
- [x] Browser/API smoke evidence is recorded.

## Proof Required

- `proof/SB05/build.txt`
- `proof/SB05/targeted-tests.txt`
- `proof/SB05/full-unit-suite.txt`
- `proof/SB05/5032-startup-log.txt`
- `proof/SB05/5032-smoke.txt`
- Optional screenshot: `proof/SB05/5032-home.png`

## Browser Validation Logging

- Route: `localhost:5032`.
- Viewport: desktop browser viewport; maximize if using Playwright.
- Required actions/assertions: navigate to the route, wait for app response, verify no browser error page, record final URL/status/title or a stable app shell selector.
- Screenshots: capture if a browser tool is available.
- Review questions: did the app load from the rebuilt process, did it show an app page rather than a proxy/error page, and are there console/startup errors that block manual testing?

## Progression Gate

- Bundle can close only when build/test/runtime proof is recorded and raw notes are no longer pending.

## Suggested Agent Prompt

```text
Implement SB05 only after SB01-SB04. Rebuild, test, start `5032`, smoke it with a real browser or HTTP probe, and update the execution report.
```
