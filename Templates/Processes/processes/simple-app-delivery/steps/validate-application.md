# Independently validate application

Remain read-only. Validate the exact approved product root and entry point. Run the build and tests required by the contract, then collect only the behavior proof applicable to the declared application kind.

- `UI`: start once when required, exercise representative behavior, capture durable browser state and a screenshot when browser-visible, inspect console errors, and stop the runtime.
- `WebApi`: start once, make bounded HTTP assertions for the declared routes and failure cases, inspect logs, and stop the runtime.
- `Console`: invoke with representative arguments, capture stdout/stderr and exit code, and assert the observable result.
- `Library`: run build and tests and verify the declared public or consumer contract. Do not require browser or standalone runtime proof.

Treat concrete product defects and missing applicable proof as `repair-required`. Use `Blocked` only when a failed tool, access, policy, or environment receipt prevents choosing a branch. Select exactly one branch: `quality-accepted` or `repair-required`.

## Evidence

Write one validation evidence artifact containing current receipts and a criterion-by-criterion verdict.
