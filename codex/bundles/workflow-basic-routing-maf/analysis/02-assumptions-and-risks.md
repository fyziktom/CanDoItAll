# Assumptions And Risks

## Assumptions

- MAF package behavior in the current repo remains compatible with documented `Microsoft.Agents.AI.Workflows` `1.3.0` APIs for `AddEdge<T>`, `AddSwitch`, and `AddFanOutEdge<T>`.
- CanDoItAll workflow node bindings will continue to use `WorkflowNodeInput` as the runtime payload wrapper in this phase.
- Simple routing can be satisfied by deterministic JSON payload checks: JSON path, operator, expected JSON value, value type, case sensitivity, and default branch metadata.
- Existing workflow definitions may contain empty or non-empty `ConditionExpression`; empty values remain direct edges, while non-empty legacy values require compatibility treatment.
- The workflow canvas can be extended without introducing a new UI library.

## Critical Path Risks

- If the domain contract in subbundle 01 is wrong, all compiler, UI, persistence, and test work becomes untrustworthy.
- If the compiler in subbundle 02 still uses the string `AddEdge` overload for conditional edges, the UI may appear correct while runtime routing remains broken.
- If switch and fan-out grouping are not deterministic, saved workflows may run different branch orders than the canvas shows.
- If the evaluator treats invalid or missing JSON as `false` without validation, workflow authors may see silent branch skips instead of actionable failures.
- If route labels are added directly to canvas link rendering without preserving existing project-structure canvas behavior, shared canvas consumers may regress.

## Validation Risks

- Browser proof requires a running app route for `/agents/workflows`; if local startup is blocked, the execution report must mark browser proof as blocked and include component-test fallback evidence.
- Durable production routing cannot be proven unless the host has a durable workflow runner available; in-process preview proof is sufficient for this bundle unless scope is explicitly widened.
- JSON-path support must remain intentionally small; over-promising full JSONPath semantics creates future compatibility debt.
- Current tests may be broad and slow; implementation agents may use targeted test selection first, but final closure needs clean relevant test proof.

## Reopen Triggers

- Reopen subbundle 01 if any persistence/API test shows route metadata is lost or old definitions cannot load.
- Reopen subbundle 02 if runtime proof shows a conditional branch executes when its predicate is false, a switch default is ignored, or fan-out target indices mismatch UI order.
- Reopen subbundle 03 if browser proof shows the route builder is clipped, unreadable, ambiguous, or forces raw JSON for normal IF/SWITCH authoring.
- Reopen subbundle 04 if migrations or stores drop `Routing` metadata, downgrade `ConditionExpression` incorrectly, or emit inconsistent API DTOs.
- Reopen the whole bundle if Microsoft changes the MAF workflow routing API signatures or if the project updates away from `WorkflowNodeInput` as the MAF-visible payload type.
