# Repair slice findings through subprocess

Launch a feature/function implementation subprocess for the concrete validation findings from `slice-repair-required`.

Carry forward:

- The chosen slice behavior and exclusions.
- Product root, app archetype, setup handoff, and architecture constraints.
- The failing proof, missing accepted child evidence, or missing test gap that triggered repair.
- The smallest repair request that can be validated by one focused proof loop.

Accepted child repair evidence can come from `feature-handoff` or `feature-handoff-after-repair`. A `feature-repair-escalation` packet is blocker evidence, not accepted repair proof.
