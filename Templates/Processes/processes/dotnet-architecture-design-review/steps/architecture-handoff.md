# Hand off reviewed .NET architecture

Summarize the accepted architecture, application type, product root, test root, UI/no-UI applicability, acceptance-driven validation plan, runtime command expectations, mandatory implementation constraints from review, and implementation slice start criteria. Preserve every `ProductAcceptanceCriteriaContract` criterion id, `kind`, and `required` value. Preserve proof mappings as acceptance obligations only for criteria with `kind=ProductAcceptance` and `required=true`, and list `kind=DeliveryPlanning` items separately as nonblocking context that cannot block handoff or trigger repair, no-go, escalation, or human reconfirmation without a separate typed decision gate. This step creates managed process artifacts only; no product files are changed.

## Downstream contract

For each required ProductAcceptance criterion or explicit product invariant, preserve the owning production boundary, implementation preconditions, proof owner, expected observable result, failure signal, and first safe repair lane. State the smallest evidence packet that a repair specialist or manager would need: failed required criterion, current proof reference, owning boundary, prior attempted change when one exists, and the unresolved hypothesis or decision. A repairable product failure must route to a bounded repair or diagnostic lane; reserve manager or human escalation for missing authority, required access, policy, contradictory requirements, an unavailable required capability, or a separate typed decision gate. Do not turn an unchanged failure signature into a sequence of indistinguishable retries.

## Contract
- Inputs: Classification, design draft, acceptance-driven validation plan, and architecture review findings.
- Outputs: Parent-ready architecture handoff for .NET implementation slice routing.
- Evidence: Accepted design, every criterion id and kind, proof mappings for required ProductAcceptance criteria, a separate nonblocking DeliveryPlanning list, unresolved risks, mandatory implementation constraints, implementation start criteria, runtime command expectations, and UI screenshot applicability.
- Operation target scope: `ExternalProductTargetReadOnly`
