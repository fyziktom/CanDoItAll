# Design acceptance-driven validation plan

Translate the reviewed scope and architecture into an implementation-ready validation plan. Map every authoritative acceptance criterion or explicit invariant to its owning production boundary, focused validation level, required input or state, expected observable proof, and concrete failure signal. Use browser or visual proof only when the delivered product has a browser-facing behavior; explicitly mark it not applicable otherwise. Do not implement or mutate product files.

This is a pre-implementation, read-only planning step. Generated product files and their build, test, runtime, or browser receipts may legitimately not exist yet. Record those as post-implementation proof prerequisites owned by downstream work; do not call them missing receipts, an unresolved blocker, or a current implementation gate, and do not return Blocked solely because they have not been produced.

## Required plan structure

For every criterion or invariant, record the primary owning production boundary, any collaborating boundary, the required initial state or input, the expected observable result, and the focused proof that can establish it. Identify separately which proof is automated, which is live-product proof when applicable, and which downstream role owns each receipt. A planned proof must name the failure signal that would invalidate the criterion and the current evidence needed to diagnose it.

For every repairable failure signal, identify the first bounded repair lane and the evidence that would make a subsequent repair materially different from a blind retry. Distinguish a product defect that should route to repair from a missing authority, access, tool, policy, or contradictory requirement that warrants a blocker or manager decision. Do not prescribe a code layout, template, fixed URL, or tool command only from the application type. If an owning production boundary does not exist yet, state the required seam and implementation constraint rather than inventing files or blocking the plan.

## Contract

- Inputs: Application classification, architecture draft, available acceptance evidence, and project structure context.
- Outputs: Acceptance-driven validation plan that implementation and QA can execute without rediscovering the proof strategy.
- Evidence: Criterion-to-proof matrix, production boundaries, validation levels, command or tool assumptions, failure signals, and non-applicable evidence classes.
- Operation target scope: `ExternalProductTargetReadOnly`
