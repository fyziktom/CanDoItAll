# SB041 — Run prepared and completed validators

## Status
Prepared.

## Objective
Close the bundle with proof index, validators, and next-bundle handoff.

## Covered Inputs
- User request to inspect latest Codex work and prepare the next bundle toward stable Process Core with domain drivers.
- Current prerequisite bundle proof from `process-driver-contract-prerequisites-verification-alpha-v1`.
- Hard safety requirement: no broad Core runtime extraction and no production driver runtime.

## Prerequisites
Previous subbundle in the same phase must be closed.

## Exact Source References
- `/src/CanDoItAll.Processes.Core`
- `/src/CanDoItAll.Processes.Contracts`
- `/src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `/tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs`
- `/tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `/codex/bundles/process-driver-contract-prerequisites-verification-alpha-v1/reviews/01-execution-report.md`

## Deliverables
- Implement or document: Run prepared and completed validators
- Update tests/source scans where applicable
- Update execution report row for SB041

## Dependency Impact
Feeds the next subbundle and the phase gate.

## Validation Depth
Focused validation plus proof transcript; deeper proof occurs at phase gate.

## Implementation Steps
1. Re-open the exact source references and the previous proof artifacts.
2. Make the smallest complete change for this subbundle.
3. Update or add architecture/unit/integration tests before closing.
4. Run the planned proof commands.
5. Record transcripts and update `reviews/01-execution-report.md`.

## Scope Exceptions
- Do not implement production runtime driver behavior unless this subbundle explicitly says so. No current subbundle approves it.
- Do not broaden Process Core into runtime orchestration.

## Do Not Do
- Do not move EF, AppDbContext, workspace/storage/filesystem, AgentFramework execution, claim lifecycle, transition execution, finalizer application, projection persistence, validation orchestration, retry scheduling, provider repair, UI/Blazor, or external service calls into Process Core.
- Production driver runtime remains out of scope: no runtime selector, registry, DI registration, manager command, shell execution driver, Office/Graph connector runtime, workspace writes, storage writes, process mutation, claim mutation, transition mutation, finalizer application, or retry scheduling.
- The only allowed production driver work in this bundle is a contract-only abstractions project if and only if architecture tests explicitly allow it and forbid runtime surfaces.
- The .NET/Rust transcript verifier work is test-only rehearsal unless SB031-SB033 explicitly approve a follow-up production alpha; this bundle must not silently ship an executable driver.
- No UI, browser, mobile, small-screen, medium-screen, screenshot, image, Razor, CSS, JS, TS, or media proof is required or allowed unless source unexpectedly touches UI surfaces; if it does, fail the bundle rather than adding mobile proof.
- Preserve existing runtime behavior; this is architecture/refactoring/contract-readiness work, not a functional rewrite.

## Acceptance Checklist
- [ ] Existing behavior preserved
- [ ] No broad Core runtime extraction
- [ ] No forbidden production driver runtime
- [ ] No UI/media drift
- [ ] Tests/scans recorded
- [ ] Execution report row updated

## Proof Required
- Build or scoped build proof
- Unit/architecture proof
- Focused process/driver prerequisite proof where relevant
- Source scans for forbidden Core/driver/UI/stub drift
- Manifest and semantic invariants for critical gates

## Browser Validation Logging
N/A. Runtime/Core/service architecture work only. If UI/media files are touched, fail the subbundle and record the unexpected drift.

## Progression Gate
Next subbundle may continue after focused proof passes.

## Suggested Agent Prompt
Implement SB041 only. Preserve existing behavior, add proof, and stop if a forbidden runtime/Core/driver side effect appears.
