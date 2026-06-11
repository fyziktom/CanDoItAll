# SB08: Release matrix + final red-team closure

## Status
Prepared.

## Objective
Run final build/test/browser/source-scan matrix and decide whether process execution is restored enough for broader branch merge planning.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts
- repo://tests/CanDoItAll.Tests.Integration
- repo://tests/CanDoItAll.Tests.Playwright

## Deliverables
- Build and full unit tests.
- Focused integration matrix for SB03-SB07.
- Large-screen Playwright proof from SB02.
- Optional live OpenAI process-template smoke if explicit opt-in env vars are present.
- Final source scans and file-size checks.
- Short decision document: “Processes restored / still blocked / merge-ready conditions”.

## Do Not Do
- Do not count skipped live OpenAI as live provider proof.
- Do not approve execution-capable drivers.
- Do not let bundle/proof files dominate source/test changes.

## Acceptance Checklist
- Build 0 errors.
- Unit tests green.
- Focused representative template automation green.
- Browser proof present for launch flow.
- Core remains generic.
- Final code-first ratio passes.

## Proof Required
- Build transcript.
- Unit transcript.
- Focused integration transcript.
- Playwright transcript/screenshots.
- Source scans.
- Final ratio calculation.

## Browser Validation Logging
Required for SB02 evidence and any run-detail/operator readback UI proof.

## Progression Gate
The branch may be considered closer to merge only if user-facing launch, representative automation, and runtime-host readback are all green. Otherwise produce explicit follow-up blockers.
