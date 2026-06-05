# Assumptions And Risks

## Working Assumptions

- The current branch is maf-processes-refactor or an equivalent continuation branch carrying the previous dispatcher refactor work.
- Existing dispatch behavior is the compatibility baseline; helper extraction must preserve wrapper entry points and observed outcomes.
- Browser validation remains N/A unless source inspection shows an unexpected UI change.

## Critical Path Risks

- **Session JSON shape drift**
   Existing code manually parses `stateBag -> InMemoryChatHistoryProvider -> messages`. A shallow extraction can change behavior by losing call/result pairing or tool-name normalization.

- **Declared outcome branch-order drift**
   Completion decisions are sensitive to declared status, selected branch outcome, repair branch recovery, terminal escalation, missing tools, and context validation errors.

- **No-progress retry fingerprint drift**
   Changing evidence signals can create either retry loops or premature compression.

- **Hidden side effects in pure helpers**
   New helpers must not write EF/storage, call execution clients, mutate agents, launch workflows, or transition steps.

- **Driver-readiness overreach**
   Driver vocabulary can be documented, but production driver contracts must wait until process runtime vocabulary stabilizes.

## Validation Risks

- Existing broad architecture tests may still include unrelated historical bundle fixture issues. Use focused tests plus source scans for this bundle, but record unrelated failures explicitly.
- Integration tests must cover both positive and negative paths: declared completed, blocked, repair disposition, invalid branch, missing tool without receipt, session tool logs, browser output references, no-progress retry compression.
- Runtime service refactor must not rely on UI proof; any UI file change is a scope violation.

## Reopen Triggers

Reopen the last production subbundle if any of these occur:

- `CanDoItAll.Processes.Core`, `IProcessDriverPack`, `IProcessDriverRegistry`, or driver packages appear.
- A pure helper uses EF, storage, service scopes, execution client, provider editor save, workflow calls, subprocess calls, or transitions.
- Completion status changes for governed blocked/completed/refused/waiting paths.
- Browser output or session tool observation loses successful tool/file evidence.
- `ToolValidation.cs` wrappers stop preserving public/internal method entry points used by tests.
- Build/test/source scans are skipped or only cited in prose.
