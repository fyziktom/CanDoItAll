# SB07 — A2A Hosting and Optional API Inventory

## Status

- `Ready after A3`

## Objective

Validate the matching 1.15 A2A preview hosting path and close the inventory of all optional 1.14/1.15 APIs without broadening the compatibility migration.

## Success Criteria

- Main A2A and Hosting.A2A packages use the matching preview build.
- Host starts and endpoint mapping remains authorized.
- Agent card/discovery, message, streaming, session, cancellation, and redacted error paths pass as applicable.
- Optional feature register has one explicit decision for each discovered feature.
- AG-UI package split is handled only if active.
- Warning suppressions are narrowed and justified.
- No optional architecture migration is smuggled into closure.

## Covered Requirements

- R02, R15, R16, R17, R20, R22

## Prerequisites

- A3 GO;
- A2A baseline fixture;
- endpoint mapping located;
- package graph stable.

## Exact Source References

- main MAF adapter project
- MAF hosting project
- `AgentFrameworkServiceCollectionExtensions.cs`
- A2A hosting registration, endpoint mapping, card factory, transport adapters, tests
- every optional-feature discovery match
- warning suppression locations

## Deliverables

- A2A smoke evidence;
- exact preview package proof;
- optional feature decision register;
- AG-UI migration only if active;
- targeted warning suppression changes;
- `proof/SB07/a2a-and-options.md`.

## Implementation Steps

1. Confirm exact resolved A2A release train.
2. Build/start host.
3. Validate agent card/discovery.
4. Validate message and stream.
5. Validate session continuity and isolation.
6. Validate approval path if exposed.
7. Validate cancellation, invalid input, authorization, and redaction.
8. Classify AG-UI use and migrate package names only if active.
9. Classify declarative workflow/autoSend.
10. Classify Harness/FileMemory/ToolApprovalAgent/message injection/compaction/CodeAct/Cosmos/Responses hosting.
11. Record `Adopt now`, `Adopt later`, `Not used`, or `Rejected`, with rationale.
12. Temporarily build without broad MAF warning suppression and narrow justified cases.
13. Add future bundles/issues for adopted-later items.

## Do Not Do

- do not convert custom Blazor activity stream to AG-UI;
- do not migrate agents to Harness;
- do not redesign as an OpenAI Responses host;
- do not introduce declarative workflows;
- do not enable compaction or FileMemory without a separate design;
- do not remove experimental suppressions without compiling all affected paths.

## Acceptance Checklist

- [ ] exact A2A preview train
- [ ] host starts
- [ ] card/message/stream pass
- [ ] session isolation
- [ ] auth/redaction
- [ ] optional register complete
- [ ] AG-UI handled if active
- [ ] warnings narrowed
- [ ] no scope expansion

## Proof Tier

- `Behavioral`

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

SB08 requires A2A health and a complete optional-feature register.

## Reopen Triggers

- A2A package build changes;
- endpoint contract changes;
- new optional package usage;
- warning set changes;
- hosting architecture proposal.

## Suggested Agent Prompt

```text
Implement SB07 only. Validate the exact 1.15 A2A preview hosting path, test card/message/stream/session/security behavior, inventory every optional 1.14/1.15 API, narrow warnings, and defer all architecture adoption not required for compatibility.
```
