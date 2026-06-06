# Suggested Implementation Agent Prompt

You are implementing `process-dispatch-projection-facet-implementation-boundary-v1`.

Follow SB01-SB84 in numeric order. Do not skip critical gates. Do not start Process Core. Do not introduce production process-driver APIs. Do not touch UI. Preserve projection source-family order and all existing behavior.

At every critical gate:
1. Run the required focused tests.
2. Run source scans.
3. Record proof under the bundle `proof/SBxx/` folder.
4. Update `reviews/01-execution-report.md`.
5. Reopen the most recent production movement subbundle if the gate fails.

Do not simplify by leaving a single class implementing all projection facets. Do not hide behavior changes under "cleanup".
