# Assumptions And Risks

## Assumptions

- The captured runs are the incident the user reported; the prior run belongs to the same session and explains the initial false claim.
- Existing Release binaries are the inspected running application. Source baseline and binary hashes must be re-anchored before execution; a diagnostic probe is not proof of a future build.
- Shared publications remain endpoint adapters. Equivalent function-call semantics are required; stochastic wording, token use, and provider-native features need not be identical.

## Critical Path Risks

- New retry logic can duplicate a committed asset if it retries unknown outcomes. Only pre-execution argument failures may trigger automatic corrected-input continuation.
- A later success for a different target must not clear an earlier failed mutation. Correlate operation/effect identity with typed evidence; tool-name equality alone is insufficient.
- Replay can cross project/agent/profile/permission boundaries or resurrect stale approval authority. Replay bounded safe evidence, never executable authority.
- Treating any historical failure as permanent failure would reject successfully corrected work. Completion must distinguish recovery, unresolved failure, cancellation, and unknown effect.

## Validation Risks

- No live shared-provider or model parity proof has been run. SB06 owns it explicitly.
- Components MCP was unavailable (Transport closed twice for inventory, once for recommendation). SB05 must inspect real component contracts before UI editing.
- Port 5032 remains stopped. Use isolated fixtures/ports/workspaces for implementation; preserve the user's original run and project.
- Existing source-only tests do not prove native or shared wire behavior. Counts and a Completed badge cannot prove stored asset bytes.
- Scoped CodeAnalytics does not establish a full-solution graph; inherited conditions and references require direct project-file inspection.

## Reopen Triggers

- Different SDK versions, changed tool schema, provider materialization, approval pipeline, history mode, or runtime scope invalidate the corresponding characterization.
- Changed public outcome serialization or persistence layout reopens SB02/SB03 and the frozen broad validation checkpoint.
- Any duplicate effect, cross-target recovery, leaked argument/credential, or wrong-project refresh reopens the responsible foundation and downstream evidence.

## MAF 1.20 upgrade risks

- Changing only the MAF stable property fails dependency closure: 1.20 requires Microsoft.Extensions.DependencyInjection.Abstractions 10.0.11 while the root pins 10.0.10.
- MEAI must move from 10.8 to 10.9 across all direct consumers. OpenAI must remain 2.12.x because MEAI OpenAI 10.9 constrains it below 2.13.
- MAF 1.19 introduced a breaking MCP Tasks extension change; no current use was found, but focused MCP lifecycle/result tests are mandatory.
- Workflow error/cancellation regression is separate from ordinary agent mutation assessment. A package upgrade cannot be accepted as proof that F02 is solved.
