# Codex execution prompt — SB00

Implement only **Baseline sync and proof reconciliation** on the current synchronized `simple-chats` branch.

## Goal

Synchronize the feature branch with current development, replace stale provenance, and classify the existing red stable-gate evidence without rerunning the whole suite.

## Success criteria

- [ ] The feature branch contains the latest development commit or an explicitly recorded equivalent merge result.
- [ ] The actual implementation head and proof head are identical and recorded.
- [ ] Every one of the 19 prior failures has a reproducible classification or is explicitly obsolete with evidence.
- [ ] No branch-induced or unresolved prior failure is deferred beyond CP0.
- [ ] No solution-wide test suite was rerun during this subbundle.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Evidence reconciliation and branch synchronization; no runtime pattern change.
- Do not add service-location, an ambient transaction, fake asynchronous fire-and-forget work, or a
  final partial-class extension.
- Run only focused validation allowed by the subbundle.
- Record exact commands, results, host, database/dependency mode and commit SHA.
- Stop on a cycle, contradiction, untrusted prerequisite, or missing required proof.
- Do not continue into the next subbundle in the same execution unless the progression record explicitly
  authorizes it.

## Required output

- coherent production/test changes for this outcome;
- updated proof manifest and handoff;
- architecture/source evidence;
- progression decision;
- honest blocker when a criterion cannot be proven.
