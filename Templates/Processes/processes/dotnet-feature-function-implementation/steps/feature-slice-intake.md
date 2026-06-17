# Capture feature or function boundary

Restate the request as one behavior that can be implemented and validated in a single subprocess run. Record acceptance criteria, exclusions, repository target, assumed existing scaffold, and the smallest proof that will demonstrate the behavior.

When the parent scope is a full app or broad deliverable, do not block only because it contains multiple future behaviors. If upstream scope, architecture, setup evidence, product root, app archetype, and validation hooks are present, derive the first reviewable MVP behavior and continue. A valid derived slice is a narrow vertical behavior that can produce a reviewable code change and proof in this subprocess.

Use upstream facts as decisions, not questions. Preserve the named product root, app archetype, target framework, test framework, UI/no-UI classification, required controls, validation hooks, and no-go constraints. Do not invent optional behavior or substitute easier contracts.

For a generated app, the first derived behavior should normally be the smallest runnable user-visible workflow that proves the app shell and primary interaction path. Record remaining requested capabilities as explicit exclusions or follow-up slice candidates.

Block only when the upstream evidence is contradictory, when product root/app archetype/validation boundary is missing, or when selecting one behavior would require inventing requirements.
