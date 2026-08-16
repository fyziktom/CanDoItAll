# C# architecture review gate

Record:

- snapshot ids and health;
- current and target owners;
- project references before/after;
- cycles before/after;
- large-file/type findings before/after;
- extracted contracts and implementations;
- tests that instantiate the new owner without the old runtime;
- old owner responsibility reduction;
- partial-class check;
- service-location check;
- source-switch/boolean-matrix check;
- source-neutral dependency check;
- progression decision.

The review fails when the new project is only a type bucket, the old monolith still owns the same presentation logic, or tests require the full Agent runtime.

## CP0 baseline decision

- Product snapshot: `snap-20260816102508-c82f9e5f`, healthy scoped graph.
- Test snapshot: `snap-20260816102634-8595e261`, healthy component-test graph.
- Current owners: AgentFramework.Components owns shared Agent presentation; Modules.AgentFramework owns orchestration and floating host; AppComponents owns source-neutral app UI.
- Project cycles: none in the scoped project graph. Two pre-existing intra-project namespace/type cycles are recorded in `proof/SB01/dependency-evidence.md` and do not cross the planned boundary.
- Hotspots: `AgentChatPanel.razor.cs` and `FloatingAgentChatHost.razor.cs` remain the principal extraction targets.
- Extracted contracts/implementations: none; SB01 forbids production changes.
- Partial-class, service-location, source-switch, and neutral-dependency checks: baseline pass; production UI exclusion scan has no Simple Chat or `Modules.LlmChats` references.
- Progression: CP0 passes to SB02. CP1 must prove the new project is executable presentation code rather than a type bucket.

## CP1 neutral-boundary decision

- After snapshot: `snap-20260816110147-d3f1a4be`, healthy, 5 scoped projects, no project cycle.
- New owner: `CanDoItAll.Conversations.Components` owns opaque presentation primitives and `PresentationBadgeList` rendering behavior.
- Dependencies: no project references; BaseLib and Blazor packages only.
- Independent proof: 7/7 neutral primitive/component tests pass without Agent runtime construction.
- Old owner reduction: intentionally none because CP1 forbids production migration; executable rendering proves the new owner is not a type bucket.
- Partial/service/source-switch checks: no partial growth, service location, runtime injection, source-kind switch, or boolean matrix.
- Progression: CP1 passes to SB03. The AllSuppliedSuites impact promotion remains scheduled for the single SB09 broad gate.

## CP3 settings and floating decision

- Before/after snapshots: `snap-20260816133136-acdf4779` and `snap-20260816134719-acdf4779`; both healthy with no blocking diagnostic.
- Ownership: neutral Conversations components own floating window/catalog/active-list presentation and lifecycle fields; Agent adapters own projections and every coordinator/context/handle/settings effect.
- Project direction: `Modules.AgentFramework -> AgentFramework.Components -> Conversations.Components`; the neutral project has no project reference to AgentFramework or LlmChats.
- Cycles: the two pre-existing intra-project module/type cycle identifiers are unchanged; no project cycle was introduced.
- Independent tests: neutral components render and route opaque callbacks without constructing the Agent runtime; Agent mapping and settings-owner tests cover the adapter boundary.
- Old-owner reduction: `FloatingAgentChatHost` no longer owns catalog window/tab/active-card/status/scroll rendering, and the settings owner no longer owns lifecycle field markup. Its code-behind decreased from 656 to 649 lines.
- Partial/service/source checks: no new partial, service location, source-kind switch, boolean matrix, persistence dependency, or backend dependency.
- Behavioral proof: 990/990 Components tests and CP3 real Agent send/hide/reopen/history/affinity/stop browser proof pass.
- Progression: CP3 passes to SB08.

## CP4 consumer and architecture closure decision

- Before/after snapshots: CP0 `snap-20260816102508-c82f9e5f`; CP4 `snap-20260816142006-84a4f698`.
- Ownership: every live Agent, contextual and Process consumer reaches the neutral renderer directly or through a purposeful Agent compatibility facade. Agent services/effects remain outside neutral UI.
- Project direction: `Modules.AgentFramework / Modules.Processes -> AgentFramework.Components -> Conversations.Components`; the neutral project has no project reference and no reverse product dependency.
- Cycles: no project cycle. The CP0 intra-project module/type cycles are unchanged and do not cross the neutral boundary.
- Duplicate/partial/service checks: superseded Agent card/list/history CSS is deleted; no second neutral implementation, new partial file, service locator, `BuildServiceProvider`, source switch, or neutral backend dependency exists.
- Testability: neutral behavior is independently tested; fresh cross-consumer tests pass 81/81; the analyzer-required Components selection passed 990/990; Processes builds with 0 warnings/errors.
- Gate result: Pass. Full record: `proof/SB08/architecture-review.md`.
- Progression: CP4 passes to SB09; Simple Chat UI remains inactive.

## CP5 final architecture decision

- Final diff: 113 files with actual one-based ranges in `proof/SB09/final-changed-files-and-ranges.json`; SB09 added no production edit.
- Snapshot: CP4 `snap-20260816142006-84a4f698` remains current because no production, project, dependency, or test input changed afterward.
- Ownership: source-neutral Conversations components own cohesive reusable presentation; Agent adapters retain runtime/domain projection; module consumers retain orchestration and effects.
- Direction/cycles: `Modules.AgentFramework / Modules.Processes -> AgentFramework.Components -> Conversations.Components`; neutral has no product ProjectReference; no project cycle.
- Independent proof: Components passed 990/990 in the final Stable gate; browser proof exercised main, floating, settings, and Process consumers.
- Old-owner reduction: superseded Agent presentation markup/CSS remains removed; no facade-only reversal or duplicate neutral implementation appeared.
- Partial/service/source checks: no added named partial file, service locator, `BuildServiceProvider`, neutral backend/persistence reference, or Simple Chat source switch.
- Stable findings: three failures are confined to untouched LlmChats integration code and do not invalidate the affected architecture boundary.
- Gate result: Pass. Stop at `awaiting-user-agent-chat-regression`; Simple Chat UI remains inactive.
