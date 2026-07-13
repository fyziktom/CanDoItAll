# Codex instructions: process runtime + MAF hardening

You are working on the `memory-providers` branch of `CanDoItAll`.

## Objective

Fix recurring blockers in large/nested process runs where a step repeats after manager rework because the process runtime cannot deterministically diagnose or satisfy artifact/tool/subprocess contracts.

The concrete symptom to protect against is:

```text
prepare-solution-skeleton is Blocked ... last strategy outcome NeedsManager ... No AgentFramework result summary was found ... verify tools/MCP/skills/project access ... can write 1 required artifact slot(s)
```

The immediate root cause is not necessarily that the .NET agent cannot scaffold a solution. The attached calculator output shows product files, but no managed process handoff artifacts. Treat this as a process-contract/handoff/diagnostics problem.

## Non-negotiable constraints

- Do not perform a broad rewrite of the process module.
- Keep process core domain-agnostic.
- Do not move .NET/domain behavior into generic runtime core.
- Do not add more huge partial classes as the main fix. New logic must be isolated into focused services/classes.
- Preserve existing UI/UX behavior except where diagnostics become more precise.
- Keep existing templates backward-compatible where possible; add new typed metadata first.
- Add tests for each fix bundle before or with implementation.
- Do not hide failures behind automatic retries.
- Avoid bundle naming leaks in production code/tests.

## Recommended branch name

```bash
git switch -c process-runtime-subprocess-artifact-hardening
```

## Implementation order

1. `B01-observability-and-diagnostics.md`
2. `B02-subprocess-runtime-bridge.md`
3. `B03-artifact-contract-and-ledger.md`
4. `B04-capability-tool-preflight.md`
5. `B05-structured-result-persistence.md`
6. `B06-template-hardening.md`
7. `B07-regression-harness.md`

Stop after each bundle and run tests. If a bundle becomes too large, split it by service boundary, not by partial class.

## Definition of done

The same `prepare-solution-skeleton` class of failure must no longer produce a generic blind-retry hint. It must produce one of these concrete states:

- parent is waiting on child run `<childRunId>`;
- child completed and parent evidence was synthesized from accepted child handoff;
- child completed with no-go escalation and parent is blocked with that concrete evidence;
- required runtime tool is missing/not composed and dispatch was prevented before agent execution;
- expected output artifact is missing and the message names the artifact expectation key/title and primary write ref;
- AgentFramework observation is missing but runtime receipt diagnostics still explain the blocker.
