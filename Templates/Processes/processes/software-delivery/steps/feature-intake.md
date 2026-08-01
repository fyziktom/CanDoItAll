# Clarify .NET scope and app type boundary

Capture the requested outcome, user or operational impact, target delivery window, known dependencies, explicit exclusions, and .NET delivery target. Classify or request classification evidence for backend-only/API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console, library, or mixed solution. Preserve explicit project-structure requirements as source-of-truth constraints; they must not be downgraded to optional, excluded, or follow-up work unless the project structure or an accepted decision record says so.

Write or update `artifacts/process-runs/<current-process-run-id>/steps/feature-intake.md` before returning. Use grounded workspace refs such as `external-target/...`, managed process refs, project-structure node ids, and source document ids; do not write native absolute product paths such as `C:\...` or `/home/...` in the artifact body, reason, summary, next actions, or final evidence refs. If a native absolute path appears in launch variables, translate it to the grounded external-target alias before recording it.

Treat the active launch request and selected project node as authoritative for this intake. If project media, imported notes, or older source documents describe another product, game, app, or domain that conflicts with the active request, record it only as excluded or stale context by project-structure id when necessary; do not carry that other domain into scope, validation hooks, downstream instructions, source citations, or final next actions.

Treat current project-source facts, including an explicitly stated delivery window, as resolved authoritative inputs. An imported open-gap, recommendation, or next action that says to validate, confirm, or reconfirm one of those same facts does not reopen it and must not become a human-decision acceptance gate. In `ProductAcceptanceCriteriaContract`, only criteria with `kind=ProductAcceptance` and `required=true` gate implementation, validation, or release. Preserve `kind=DeliveryPlanning` items in the scope packet as nonblocking planning context; they cannot cause `NeedsManager`, `WaitingApproval`, or human-confirmation escalation unless the process supplies a separate typed decision gate for that exact decision.

Return `Succeeded` / `Completed` once the grounded scope packet is written. Do not return `NeedsManager`, `WaitingApproval`, `Failed`, or `Blocked` only because optional source documents conflict with the active request, because downstream implementation/QA proof is pending, or because native paths had to be translated to grounded aliases.

## Contract
- Inputs: Requested change, impact notes, target delivery window, and stakeholder-facing constraints.
- Outputs: Decision-ready .NET scope packet with acceptance boundary, app-type hypothesis, dependency map, assumptions, exclusions, and validation hooks.
- Evidence: Intake notes, acceptance criteria, .NET app-type hypothesis, product root hints, UI/no-UI hints, run/test command hints, known exclusions, assumptions, and unresolved dependency register.
- Operation target scope: `ExternalProductTargetReadOnly`
