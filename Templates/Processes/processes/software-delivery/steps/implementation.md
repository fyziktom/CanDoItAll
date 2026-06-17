# Run .NET implementation slice subprocess

Launch and observe the .NET implementation slice subprocess for the approved scope and architecture. The child implementation slice owns solution setup, feature/function implementation, bounded repair subprocesses, tests, targeted proof, and accepted handoff evidence. This parent step records child-run evidence and does not mutate product files directly.

When the approved scope is a full app or broad deliverable, launch the child with a first reviewable MVP implementation slice derived from the feature-intake and architecture artifacts. Keep later runtime command writeback, screenshot writeback, security, release, and follow-up feature slices in their own downstream steps instead of forcing all work into this parent step.

Accepted child evidence can come from `slice-handoff` or `slice-handoff-after-repair`. A `slice-repair-escalation` packet is blocker/no-go evidence, not accepted implementation proof.

## Contract
- Inputs: Approved .NET architecture path, app classification, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Outputs: Observed child implementation slice with reviewable change set, test evidence, blockers, rollout inputs, accepted parent-ready handoff, or explicit repair escalation evidence.
- Evidence: Child run status, change-set projection, validation outputs, accepted handoff evidence, repair escalation evidence, output-placement notes, migration steps when applicable, touched-surface inventory, and blockers.
- Operation target scope: `ExternalActionControlled`
