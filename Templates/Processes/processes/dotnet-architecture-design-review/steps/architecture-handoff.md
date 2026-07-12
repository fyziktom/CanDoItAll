# Hand off reviewed .NET architecture

Summarize the accepted architecture, application type, product root, test root, UI/no-UI applicability, acceptance-driven validation plan, runtime command expectations, mandatory implementation constraints from review, and implementation slice start criteria. Preserve the criterion-to-proof mapping so downstream implementation and QA do not silently narrow the scope. This step creates managed process artifacts only; no product files are changed.

## Downstream contract

For each criterion or invariant, preserve the owning production boundary, implementation preconditions, proof owner, expected observable result, failure signal, and first safe repair lane. State the smallest evidence packet that a repair specialist or manager would need: failed criterion, current proof reference, owning boundary, prior attempted change when one exists, and the unresolved hypothesis or decision. A repairable product failure must route to a bounded repair or diagnostic lane; reserve manager or human escalation for missing authority, required access, policy, contradictory requirements, or an unavailable required capability. Do not turn an unchanged failure signature into a sequence of indistinguishable retries.

## Contract
- Inputs: Classification, design draft, acceptance-driven validation plan, and architecture review findings.
- Outputs: Parent-ready architecture handoff for .NET implementation slice routing.
- Evidence: Accepted design, criterion-to-proof mapping, unresolved risks, mandatory implementation constraints, implementation start criteria, runtime command expectations, and UI screenshot applicability.
- Operation target scope: `ExternalProductTargetReadOnly`
