# 07 Documentation, API Skills, and Runtime Closure

## Status

- `Completed`
- Gate: `A7 GO with three inherited A5 P2 follow-ups`

## Objective

- Align product and SharedInfo architecture/API/skill documentation, validate one real low-cost agent flow, run final builds/tests/reviews, and leave a healthy rebuilt host on port 5032.

## Success Criteria

- Product docs describe event/snapshot/preparation ownership, lifecycle, metrics, extension guidance, and migration.
- SharedInfo API docs/skills describe typed activity contracts and future authorized SSE projection without claiming an endpoint exists.
- One real tool-free agent invocation uses `gpt-5.4-mini` and proves activity-to-run/provider/stream/completion wiring.
- Final solution builds/tests, CodeAnalytics/architecture review, bundle validators, documentation validators, and health checks pass.
- Port 5032 is restarted from the rebuilt state and responds.

## Covered Inputs

- R11-R14, SharedInfo update, mini-model instruction, final rebuild/restart, complete raw-note closure.

## Prerequisites

- SB06 A6 gate passes.
- SharedInfo path and applicable `AGENTS.md`/documentation standards are revalidated.

## Exact Source References

- `C:\repositories\CanDoItAll\docs`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web`
- `C:\repositories\CanDoItAll.SharedInfo\docs`
- `C:\repositories\CanDoItAll.SharedInfo\codex\skills`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`

## UI Composition Contract

- No new UI composition. Recheck SB06 surfaces at large desktop after final rebuild and restart.

## Deliverables

- Product architecture/API/module documentation.
- SharedInfo API documentation and relevant skill updates.
- Real `gpt-5.4-mini` validation transcript and activity timing.
- Final build/test/architecture/doc-validator proof.
- Rebuilt restarted host and port-5032 health artifact.
- Closed traceability and execution report.

## Dependency Impact

- This is final closure. Documentation drift or a failed real/runtime/host check means the implementation is not operationally handed off.

## Validation Depth

- Proof tier: `Governed`.
- Final cross-repository, architecture, runtime, UI, and deployment-readiness gate.

## Implementation Steps

1. Discover exact product and SharedInfo docs/skills affected; update contracts/examples/lifecycle and future SSE notes.
2. Run SharedInfo documentation/skill validators and product doc checks.
3. Configure a temporary/minimal active agent for `gpt-5.4-mini`, disable tools, use a tiny exact-response prompt, and run once without retries.
4. Capture accepted/planning/framework/provider/streaming/completion activities and usage.
5. Run targeted/full tests, `dotnet build CanDoItAll.slnx`, final CodeAnalytics/dependency and C# architecture review.
6. Rerun bundle validators and semantic/anti-stub/traceability closure.
7. Resolve exact existing port-5032 process, stop it safely, start rebuilt host, and verify HTTP/browser health.
8. Complete A7 and final execution report.

## Scope Exceptions

- No SSE endpoint, MQTT/OPC UA transport, distributed broker, or production provider/profile migration.

## Do Not Do

- Do not run Terra or retry the measured real call.
- Do not change persisted user configuration as a test side effect.
- Do not claim API availability that is only an architectural seam.
- Do not stop an unidentified process or restart from stale binaries.

## Acceptance Checklist

- [x] Product and SharedInfo docs/skills agree with source.
- [x] Mini-model configuration and one-call result are explicit; no Terra call or retry occurred.
- [x] Final architecture snapshot has no new affected-project cycle or critical finding.
- [x] Required build, focused test suites, and SharedInfo validators pass.
- [x] Browser evidence matches the final typed UI composition and has no console/runtime error.
- [x] Port 5032 runs rebuilt code and returns healthy responses.
- [x] Every R01-R14 row is closed with proof.

## Proof Required

- `proof/SB07/manifest.md`, documentation diff/validator logs, mini-model transcript/usage/timings, build/test logs, final architecture snapshot/review, final browser health screenshot, process/start/HTTP logs, semantic invariants, anti-stub and closure audit.

## Browser Validation Logging

- Reuse SB06 routes at `1920x1080` after the rebuilt 5032 host starts.
- Assert application health, floating/process activity feedback, terminal state, and no console/runtime error.
- Capture final health and one busy-state screenshot; record scroll owner and visual diff findings in the execution report.

## Progression Gate

- A7 passed. Closure evidence is indexed in `proof/SB07/manifest.md`; the three
  inherited A5 P2 follow-ups remain bounded and explicit.

## Reopen Triggers

- Any failed validator/build/test, Terra usage, source-doc mismatch, missing event in the real run, or unhealthy/stale 5032 instance reopens the owning subbundle and blocks completion.

## C# Architecture Contract

- Documentation names actual source owners and durability/lifetime semantics.
- API docs distinguish current in-process contract from future authorized SSE projection.
- Final architecture review covers dependencies, singleton/scoped lifetimes, disposal, snapshot source-of-truth, and UI boundaries.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Close documentation, real mini-model wiring, builds/tests/reviews, and rebuilt 5032 health with governed proof; reopen earlier work rather than declaring partial completion.
```
