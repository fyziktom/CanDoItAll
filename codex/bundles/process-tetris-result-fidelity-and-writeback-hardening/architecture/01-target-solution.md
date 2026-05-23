# Target Solution

## Runtime Governance

Final writeback steps stay governed by required tool rules, but real tool failures must be visible to those rules. The target behavior is:

1. The MAF project-structure tool wrapper calls the runtime gateway.
2. If the gateway throws a project-structure agent exception or other expected tool exception, the execution detail records a failed receipt with the tool name, safe error code, and safe message.
3. Process completion evaluation distinguishes:
   - blocked claim with no failed receipt: invalid process outcome,
   - blocked claim with failed receipt: valid blocked/recovery path,
   - completed claim without required project-structure tool success: failed outcome.
4. Recovery packets include the failed tool receipt and exact source paths/target node ids when safe.

No hidden fallback should mark writeback complete without a project-structure node or explicit operator escalation.

## Contract Fidelity

The `Blazor delivery contract` becomes a hard downstream input, not a summary hint. Implementation and validation must preserve:

- selected mode (`WASM` in this run),
- static/no-backend constraints,
- product root/run folder,
- route and persistence requirements,
- exclusions such as no SSR.

If implementation cannot honor the contract, it must return a blocked outcome asking for a contract revision rather than switching to a different root or hosting model.

## Browser Semantic Proof

Browser proof must be domain-aware for game delivery. A passing Tetris proof should include:

- route and viewport,
- screenshot and accessibility snapshot,
- console capture,
- status after hydration/startup,
- keyboard sequence and resulting observable state change,
- localStorage high-score write/read,
- proof that no backend/API dependency is needed for gameplay or score persistence.

This should be encoded in QA prompts and, where feasible, runtime validation heuristics/tests.

## Final Rerun Closure

After SB01-SB03 pass, rerun the process through the public APIs. Closure requires:

- process run terminal success,
- all required artifact expectations satisfied,
- no open escalation/dead-lettered outbox,
- final verdict/evidence index written under the target `Main app` node,
- final app proof showing static/no-backend Tetris behavior.

The final rerun is proof of process hardening, not a substitute for the focused tests in earlier subbundles.
