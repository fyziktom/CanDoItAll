# Assumptions And Risks

## Working Assumptions

- Project hierarchy is a directed acyclic graph. Multiple parents are allowed; self-links and cycles are not.
- The Projects page can reuse a single modal shell for recursive related-project navigation as long as context is preserved and reopening subprojects is obvious.
- The structure canvas does not need to render every transitive descendant on one surface; it needs direct parents, direct children, and the extra-parent context required to explain multi-parent children, plus new-tab traversal to deeper project canvases.
- Existing project search indexing and activity logging must continue to work after hierarchy fields are introduced.

## Critical Path Risks

- If the relation model is added in the wrong place, downstream workbench sync will either duplicate project data or require a late refactor.
- If workbench projection tries to overload `ParentNodeKey` for multi-parent project relations, the data model will stay inconsistent and reconnect behavior will remain ambiguous.
- If reconnect is implemented as delete-and-recreate without guardrails, user-authored links or canvas coordinates may drift unexpectedly.
- If the repo-local skill pack remains incomplete, the process improvements discovered in this run will not ship to other machines.

## Validation Risks

- Recursive project modal flows can look correct in markup but still fail visually through clipping, confusing navigation state, or unreadable hierarchy cues.
- Secondary-parent canvas nodes can be technically present but visually unclear unless the subdued styling is obvious on a real browser capture.
- New-tab actions can appear to work in component tests but still open the wrong route or reuse the current tab in a real browser.
- Final closure can still be weak if analytics rows are present but do not contain real route interactions, screenshots, or explicit gate results.

## Reopen Triggers

- Reopen subbundle 01 if hierarchy persistence allows a cycle, allows self-parenting, or fails to provide enough typed data for Projects page and canvas consumers.
- Reopen subbundle 02 if related-project filtering or the subproject modal cannot prove recursive navigation and parent discovery in a real browser.
- Reopen subbundle 03 if extra-parent nodes are not visually distinguishable, if reconnect leaves stale relations, or if new-tab route actions are wrong.
- Reopen subbundle 04 if any raw note remains partially solved without a documented follow-up or if screenshot review exposes layout problems.
- Reopen subbundle 05 if validator skills are still missing from the repo skill pack or the install script still cannot propagate the changed skills.
