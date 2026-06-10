# Phase plan

## Phase Sequence
- Execute SB01 through SB12 in dependency order.
- Do not start a subbundle until its predecessor closure gate passes or is honestly blocked with a follow-up path.

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 Code-first baseline and ratio guard]
  SB02[SB02 Runtime dry-run contracts]
  SB03[SB03 Durable audit hardening]
  SB04[SB04 Host status/operator API]
  SB05[SB05 Scheduler/workflow read-only jobs]
  SB06[SB06 Sandbox and authorization evaluator]
  SB07[SB07 Static driver capability descriptors]
  SB08[SB08 Manager run-detail readback]
  SB09[SB09 Live OpenAI process-run hardening]
  SB10[SB10 Deterministic process regression]
  SB11[SB11 Core genericity and boundary guards]
  SB12[SB12 Release candidate and code-first red-team]

  SB01 --> SB02 --> SB03 --> SB04 --> SB05 --> SB06 --> SB07 --> SB08 --> SB09 --> SB10 --> SB11 --> SB12
```

## Critical Subbundles
- SB01 through SB12 are critical. This bundle has fewer subbundles; each owns a larger coherent implementation area.

## Phase Gates
- Prepared-stage bundle validator passes before implementation.
- Each subbundle has an entry gate, focused implementation, proof manifest, semantic invariant contract, closure gate, and downstream progression decision.
- Completed-stage bundle validator and code-first ratio gate pass before final closure.

## Final validation matrix
- `dotnet build CanDoItAll.slnx --configuration Debug`
- full unit tests
- focused integration tests for verification host, dry-run host, audit, manager facade, scheduler/workflow read-only jobs
- live OpenAI process-run smoke when opt-in variables are present
- large-screen operator readback smoke when UI/API route changes are made
- source scans for Core dependency drift, reflection discovery, fallback selector, mutation APIs, bundle-path coupling, secret leakage
- code-first ratio gate with `git diff --numstat`
