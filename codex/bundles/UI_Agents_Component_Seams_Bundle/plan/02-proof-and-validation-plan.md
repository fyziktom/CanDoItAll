# Proof and validation plan

## Focused proof

- production build of `CanDoItAll.Modules.AgentFramework` after each source slice;
- Unit route/state/controller/dependency filters with exact discovery;
- Components home/catalog/details filters with exact discovery;
- direct negative checks for forbidden test and Razor dependencies;
- DI resolution smoke for all three new seams.

## Broad gate decision

A final stable aggregate is required because the bundle changes shared AgentFramework UI
composition and service registration used by the Web host and test harness. It is not
required after every subbundle.

## Browser/host proof

At SB07, run the real application at a large desktop viewport (recommended 1600x1000 or
larger) and capture:

1. `/agents` Overview loaded;
2. Agents catalog with selection and managed-chat action visible;
3. agent details open on a non-default typed section;
4. a current `agentId` deep link opening once;
5. save or cancel/close returning to a usable catalog.

Check dialog clipping, action visibility, internal scroll owner, console errors, and
Blazor error UI. Do not add tablet/mobile tuning or unquarantine the existing broad AI
agent Playwright flow for this architecture-only change.

## Portability proof

Run the mandatory portability-static procedure after final source edits. Review every
ADDED/STALE delta; refresh the baseline only for intentional findings and rerun final
no-write enforcement.

## Architecture proof

- dependency before/after table;
- source assertions that moved calls left Razor;
- direct tests of each new seam;
- no new partial/project reference;
- no private-reflection/uninitialized-service target tests;
- CodeAnalytics snapshot/cycle evidence when available;
- completed C# architecture gate.
