# Normalized Requirements

## Node Inventory And Semantics Requirements

- `R001` The bundle and later implementation must inventory every process-canvas node family currently projected by the process module and identify the intended editable input and output ports for each family.
- `R002` The bundle and later implementation must classify each editable connection family as `many-to-many`, `single-to-many`, `many-to-single`, or `single-to-single`.
- `R003` The node and port inventory must remain strongly typed and must not rely on scattered magic strings for responsibility kinds, artifact flows, or node-family semantics.
- `R004` The node and port inventory must include applicable step-kind exceptions where a port should be suppressed or treated differently, especially for `Start`, `Decision`, and `End`.

## Canvas-Primary Authoring Requirements

- `R005` The process canvas must move from a branch-router-special-case editor toward a primary editor for process definitions.
- `R006` Process steps must gain explicit structural and participation semantics on the canvas rather than remaining generic single-anchor nodes.
- `R007` Process roles must expose participant-role outputs in addition to decision-authority output.
- `R008` Branch routers must stay additive and compatible with the generalized multi-port contract rather than staying as a one-off feature.
- `R009` Runtime nodes must eventually project enough authored semantics that the resulting process is understandable after execution starts.

## Participant And Cardinality Requirements

- `R010` Role-to-step participant connections must support the canonical responsibility kinds `Responsible`, `Reviewer`, `Approver`, and `Backup`.
- `R011` Role participation must be modeled as a many-to-many graph overall even when each concrete port may behave as many-to-single on the step side.
- `R012` Decision-authority assignment must remain a singular target-side relation unless the canonical model changes explicitly.
- `R013` Step structural dependencies must preserve existing support for many upstream dependencies and one-to-many downstream fan-out.
- `R014` Branch outcome routing must remain explicit per outcome and must continue to support later joins where one step waits on multiple upstream flows.

## Artifact And Contract Requirements

- `R015` The bundle must identify whether artifact expectations can remain step-owned metadata or must become explicit graph links for honest canvas-first authoring.
- `R016` If explicit artifact consumption is required to make the canvas primary, the canonical model and persistence layer must be extended rather than faked in UI state.
- `R017` Artifact-related ports must be classified by cardinality and by whether they are grouped or per-artifact-instance badges.

## Persistence And Canonical Truth Requirements

- `R018` Every canvas-authored relationship and node move covered by this initiative must round-trip through the service layer and database without snapping back after later interactions.
- `R019` The bundle must explicitly call out which relationship families are already canonical and which need new persistence support.
- `R020` Execution must not claim success for a canvas feature whose authored state disappears after save, reload, or later projection rebuild.

## Scenario And Validation Requirements

- `R021` Execution must validate the work on realistic software-development scenarios, not just synthetic single-branch demos.
- `R022` The target scenarios must include review, QA, approval, and rework style flows where role participation and many-to-many joins matter.
- `R023` UI-bearing phases must be validated with Playwright MCP on `/processes`, large-screen screenshots, and screenshot review.
- `R024` Final closure must show that the canvas can author substantially more of the process graph than before and must identify any residual form-only exceptions honestly.
