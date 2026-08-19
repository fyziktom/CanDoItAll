# Change control

## Locked scope

This bundle owns backend, persistence, composition, HTTP API, and non-UI proof for simple LLM chats.

## Scope changes requiring an explicit architecture decision

Record a decision in `architecture/10-decision-register.md` before doing any of the following:

- adding or changing a project boundary;
- introducing a direct dependency on agent execution, MAF, tools, skills, MCP, Memory, Processes, or UI;
- exposing a new public API route;
- changing transcript semantics or optimistic-concurrency behavior;
- changing the database table set or deletion/retention semantics;
- introducing a queue, background worker, event bus, streaming protocol, or external channel;
- accepting raw provider SDK settings or credentials from the HTTP API;
- adding a concrete context or attachment source;
- weakening profile-generation fencing;
- running a broad test command before SB11.

## Deviation process

A deviation entry must include:

1. originating subbundle;
2. force that makes the prepared design invalid;
3. alternatives considered;
4. dependency impact;
5. migration/compatibility impact;
6. test evidence required;
7. checkpoint that must reopen.

“No time” and “simpler for now” are not sufficient architecture forces.

## Authorized follow-up decision

The operator's preserved thinking-effort follow-up authorizes ADR-013 and the safe provider-options
projection under the LLM Chat route family. It does not authorize agent execution dependencies,
credentials/configuration exposure, per-turn model overrides, or a second capability catalog.
