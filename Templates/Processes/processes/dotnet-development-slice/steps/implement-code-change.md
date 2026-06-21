# Implement bounded code change through feature/function subprocess

Launch the feature/function implementation subprocess for the bounded behavior in this slice. Keep the parent step focused on observing the child run status, change-set artifact, targeted validation evidence, accepted handoff evidence, repair escalation evidence, blockers, and manager rework directives.

Before launching the child, carry forward one concrete feature request from the slice scope and architecture artifacts. Include the product root, app archetype, setup handoff, acceptance criteria, validation hooks, and exclusions. When the parent slice came from a full app request, the child request must be the derived first MVP behavior, not the whole app backlog.

Do not launch the child when the slice scope only asks for scaffold readiness, solution setup, naming, or generic app-shell existence. A scaffold is prerequisite evidence, not an accepted implementation behavior. Reopen or block the slice intake unless the child request names an observable product-specific behavior and the acceptance checks for that behavior.

Accepted child evidence can come from `feature-handoff` or `feature-handoff-after-repair`. A `feature-repair-escalation` packet is blocker evidence, not accepted implementation proof. If the child run is complete but only escalation/no-go evidence exists, record the blocker and do not mark this step accepted.
