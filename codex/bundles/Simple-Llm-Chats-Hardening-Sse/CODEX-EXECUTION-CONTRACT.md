# Codex execution contract

## Mandatory posture

- Work in `fyziktom/CanDoItAll` on the hardening branch selected from `simple-chats`.
- Start SB00 by synchronizing with current `development` in a clean worktree.
- Treat current repository source/tests as authority; bundle paths are discovery anchors.
- Load the SharedInfo skills listed in `source/04-sharedinfo-skills-used.md`.
- Do not implement UI, Project Structure context, agent integration, or deployment channels.
- Do not add partial classes as a final boundary.
- Do not use `IServiceProvider` service location in core/application behavior.
- Keep provider I/O outside database transactions.
- Make durable transitions independently testable without the Web host.

## Evidence

- Update the active subbundle README status, `SESSION-HANDOFF.md`, and proof manifest while evidence is fresh.
- Record actual SHA, dependency mode, OS, database, command, exit code, and artifact path.
- Do not reuse original SB11 evidence for a changed commit.
- A checkpoint unlocks downstream work only after every owned criterion is proven.
- A code change after a checkpoint reopens its owner and invalidates dependent proof.

## Test discipline

- Follow `test-budget.json`.
- SB00-SB12: affected builds and filtered tests only.
- SB13: one stable filtered solution run at the immutable final commit, then one CI matrix run.
- Never run the unfiltered suite or Playwright in this bundle.
- Never rerun an unchanged broad failure merely to seek a different result.

## Architecture stop rules

Stop and redesign if:

- product/transcript writes cannot share one explicit transaction command;
- terminal operation can coexist with an unresolved active turn;
- cancellation can become success;
- liveness is inferred from a process-local dictionary;
- current profile is looked up again after admission instead of using the captured scope;
- request disconnect owns durable cancellation;
- streaming contracts depend on HTTP/SSE/Web types;
- deltas are stored one row per token;
- a stream retries after emitting output;
- Web/API types leak into product/provider contract projects.
