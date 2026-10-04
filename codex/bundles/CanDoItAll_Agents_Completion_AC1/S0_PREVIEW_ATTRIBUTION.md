# WF1-R1 — reserved preview identity belongs to one attempt

## Source-derived defect

`WorkflowCanvasEditor` constructs one `WorkflowPreviewOwner` for the editor lifetime (S05). In that owner (S04), a completed preview sets both `Accepted` and `ReservedRunId`. Starting the next preview sets `pending` but does not establish a fresh attempt-local reserved identity. If the next `runner.RunAsync` call throws a generic exception after `dispatched=true`, the catch returns `Unknown(ReservedRunId)` using the previous run's ID.

Reproduction sequence: preview A returns run RA; preview B on the same owner enters the runner and loses acknowledgement through a generic exception; B returns Unknown(RA). This is a wrong result attribution, not proof that RA was run twice or that B had no effect. The current UI accepts the unknown outcome's reserved identity (S06). No runtime reproduction was executed by this reviewer.

## Smallest valid repair

Separate the last accepted historical result from the active attempt's known reservation. Initialize/capture current-attempt state before dispatch. Attach an ID to Unknown only if that attempt's native admission evidence supplied it (including the dedicated observation exception). An unconfirmed call with no known current identity must return no reservation, not Guid.Empty or the prior successful ID.

Do not erase historical accepted facts to make the comparison pass. Do not query the latest run by name/time and adopt it as proof. Preserve native `WorkflowLaunchAdmissionObservationException`, unknown-replay guard, exactly one admitted current request, explicit before-dispatch authority refusal and immutable definition/input/version.

## Proof

Use the real owner with controllable boundary delegates and real production composition where needed: A successful then B generic failure; A success then B dedicated observation exception with RB; A success then authority failure with zero second dispatch; B success; repeated calls while B pending; retry after unknown without new dispatch; retirement before authority capture finishes; two owners with similar definitions. Assert both returned outcomes and native owner/consumer facts, not just status text.

Run both canvas preview and page preview entry paths. Verify fresh version/input attribution and retained A read-back; B must never offer A as its recovery target. Native tests should separately cover an accepted run whose secondary observer/read fails, using the actual admission result contract.

Also retain the existing late-progress/queued-renderer callback tests while touching this family. Any newly reproduced stale progress after document replacement should be fixed narrowly with the occurrence check at application of the callback; this is an additional regression requirement, not a separately established finding in this review.
