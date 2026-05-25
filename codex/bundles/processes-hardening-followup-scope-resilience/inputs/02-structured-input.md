# Structured Input

| Note ID | Raw intent | Normalized requirement |
| --- | --- | --- |
| N001 | Codex implemented the previous bundle in `processes-hardening`; review it. | Ground current-state analysis in branch `processes-hardening`, not assumptions from the previous bundle. |
| N002 | Analyze the whole mechanism of process execution. | Cover dispatch, direct agents, workflows assigned as roles, subprocesses, artifact projection, finalization, recovery, retries, prompts, tool policy, and process definitions. |
| N003 | Find weak spots that cause unnecessary stopping/blocking. | Identify hard-block conditions that should instead become repair branch, waiting state, diagnostic, manager recovery, or retry compression. |
| N004 | Architecture step started implementation too early. | Add explicit step execution boundary and tool-level operation policy so non-mutating steps cannot mutate product targets. |
| N005 | Process core must stay generic. | Avoid Blazor-specific hardcoding; model generic operations, artifact modes, role/step policies, and branch dispositions. |
| N006 | Much depends on instructions/definitions. | Add process definition lint/simulation and template QA so bad process definitions are caught before runtime. |
| N007 | Prepare a follow-up bundle as ZIP. | Produce an implementation-ready Codex bundle with subbundles, gates, proof, and tests. |
