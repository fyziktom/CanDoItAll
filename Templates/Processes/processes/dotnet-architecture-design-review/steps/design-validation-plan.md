# Design acceptance-driven validation plan

Translate the reviewed scope and architecture into an implementation-ready validation plan. When `ProductAcceptanceCriteriaContract` is present, preserve every criterion id in the plan, but require an owning production boundary, proof, and failure signal only for criteria with `kind=ProductAcceptance` and `required=true`. Record `kind=DeliveryPlanning` items separately as nonblocking planning context; they require no product proof and cannot create a repair, no-go, escalation, or human-confirmation gate unless a separate typed decision gate explicitly requests that decision. Map every other explicit product invariant to its owning production boundary, focused validation level, required input or state, expected observable proof, and concrete failure signal. Use browser or visual proof only when the delivered product has a browser-facing behavior; explicitly mark it not applicable otherwise. Do not implement or mutate product files.

This is a pre-implementation, read-only planning step. Generated product files and their build, test, runtime, or browser receipts may legitimately not exist yet. Record those as post-implementation proof prerequisites owned by downstream work; do not call them missing receipts, an unresolved blocker, or a current implementation gate, and do not return Blocked solely because they have not been produced.

## Required plan structure

For every required ProductAcceptance criterion or explicit product invariant, record the primary owning production boundary, any collaborating boundary, the required initial state or input, the expected observable result, and the focused proof that can establish it. Preserve all other criterion ids in the separate nonblocking DeliveryPlanning section. Identify separately which proof is automated, which is live-product proof when applicable, and which downstream role owns each receipt. A planned proof must name the failure signal that would invalidate the required ProductAcceptance criterion and the current evidence needed to diagnose it.

For every repairable failure signal, identify the first bounded repair lane and the evidence that would make a subsequent repair materially different from a blind retry. Distinguish a product defect that should route to repair from a missing authority, access, tool, policy, or contradictory requirement that warrants a blocker or manager decision. Do not prescribe a code layout, template, fixed URL, or tool command only from the application type. If an owning production boundary does not exist yet, state the required seam and implementation constraint rather than inventing files or blocking the plan.

## Contract

- Inputs: Application classification, architecture draft, available acceptance evidence, and project structure context.
- Outputs: Acceptance-driven validation plan that implementation and QA can execute without rediscovering the proof strategy.
- Evidence: Every criterion id and kind, proof mappings for required ProductAcceptance criteria and explicit product invariants, a separate nonblocking DeliveryPlanning list, production boundaries, validation levels, command or tool assumptions, failure signals, and non-applicable evidence classes.
- Operation target scope: `ExternalProductTargetReadOnly`
