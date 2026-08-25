# Closure

State: `NOT_EXECUTED`

Final closure is owned by SB12. It must classify every requirement in
`traceability/01-requirements-matrix.md` as `Solved`, `Partially solved`, or `Not solved`,
with durable evidence.

The final report must also state:

- final CanDoItAll and SharedInfo commits/worktree state;
- the one stable aggregate result;
- the final multi-instance result;
- OpenAPI snapshot provenance and hashes;
- SharedInfo validator results;
- running container names, health, and URLs;
- manual-test handoff path;
- cleanup command not executed;
- any explicit blocker or residual risk.

No success language belongs here before execution.

`Partially solved` and `Not solved` are honest blocked-handoff classifications only. Final
`DONE` and success language require every mandatory requirement to be `Solved` with evidence.
