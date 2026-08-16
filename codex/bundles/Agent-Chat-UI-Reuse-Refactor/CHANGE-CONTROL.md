# Change control

## Preparation baseline

- Branch: `simple-chats`
- Observed head: `eca249942211d9d8839f3e0da9b1997b7d652684`
- Parent bundle commit: `c3c7713927b9519200900583f227ead95fafb5e9`
- SharedInfo observed head: `7b7808e8591d7219f40826cf0e5624e182981d90`

## Drift rules

At SB01 entry:

1. fetch the live `simple-chats` branch;
2. record the actual execution base SHA;
3. compare the source owners, project graph, current tests, and current bundle/SharedInfo instructions against this preparation baseline;
4. classify drift as:
   - **compatible**: source moved or changed without altering the planned responsibility split;
   - **requires bundle repair**: ownership, dependency direction, component public contracts, consumers, or test-selection rules materially changed;
   - **blocked**: backend closure or current Agent Chat behavior is not stable enough for a behavior-preserving UI refactor.

Do not silently rebase the plan over material drift.

## Scope expansion

Any of the following requires reopening the current checkpoint or repairing the bundle:

- a new production consumer of current Agent Chat components;
- a new project reference outside the approved dependency plan;
- Simple Chat UI or API consumption;
- changes to persistence, API, SSE, provider drivers, or operation state machines;
- a public component contract change that cannot be hidden by a compatibility facade;
- a broad test promotion trigger;
- a cycle or reversed dependency;
- an Agent Chat behavior change discovered during refactoring.
