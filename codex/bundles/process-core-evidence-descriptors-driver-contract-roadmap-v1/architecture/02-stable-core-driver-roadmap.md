# Roadmap Toward Stable Process Core With Domain Drivers

## Stage A — Core pure rule stabilization
1. Route/subprocess/artifact pure rules are already seeded.
2. Add execution/finalizer evidence descriptors.
3. Add stable diagnostics and reason codes.
4. Enforce public API snapshot and consumer allow-list.

## Stage B — Core descriptor expansion
5. Add validation/projection evidence descriptors without storage/workspace IO.
6. Add retry/provider/no-progress diagnostic facts without AgentFramework runtime dependency.
7. Add finalizer intent/outcome descriptors without finalizer application.

## Stage C — Driver contract preparation
8. Define production driver contract proposal, still docs/tests only.
9. Define permission modes: `VerificationOnly`, `ManagerReadonly`, `ExecutionCapableFuture`.
10. Define capability scopes, audit facts, denial reasons and output schemas.
11. Define sandbox/command policy for any future execution-capable lane.

## Stage D — Domain driver lanes
12. .NET/Rust verification lanes: inspect build/test/proof outputs only.
13. Office lane: inspect email/document metadata/proof facts only; no Graph runtime in first proposal.
14. Business-analysis lane: inspect deliverables/requirements/checklists only; no business-record mutation.
15. Later, after explicit approval, implement one verification-only driver as a production alpha.

## Stage E — Runtime integration
16. Add runtime driver registry only after contract + permission + audit + sandbox are complete.
17. Keep mutation-capable execution under process module ownership.
18. Never let drivers bypass claims, transitions, finalizer, storage, or process audit.
