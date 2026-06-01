# Clarify .NET scope and app type boundary

Capture the requested outcome, user or operational impact, target delivery window, known dependencies, explicit exclusions, and .NET delivery target. Classify or request classification evidence for backend-only/API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console, library, or mixed solution. Preserve explicit project-structure requirements as source-of-truth constraints; they must not be downgraded to optional, excluded, or follow-up work unless the project structure or an accepted decision record says so.

## Contract
- Inputs: Requested change, impact notes, target delivery window, and stakeholder-facing constraints.
- Outputs: Decision-ready .NET scope packet with acceptance boundary, app-type hypothesis, dependency map, assumptions, exclusions, and validation hooks.
- Evidence: Intake notes, acceptance criteria, .NET app-type hypothesis, product root hints, UI/no-UI hints, run/test command hints, known exclusions, assumptions, and unresolved dependency register.
- Operation target scope: `ExternalProductTargetReadOnly`
