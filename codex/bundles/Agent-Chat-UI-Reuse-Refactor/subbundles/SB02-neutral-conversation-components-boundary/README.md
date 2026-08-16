# SB02 — Neutral Conversation Components boundary

## Status

`pending`

## Proof tier

`Governed`

## Dependency

- Depends on: SB01
- Closure checkpoint: CP1
- Owned requirements: UIR-010, UIR-011, UIR-012, UIR-013, UIR-014, UIR-015, UIR-016, UIR-017, UIR-018, UIR-026, UIR-070, UIR-073, UIR-074, UIR-077

## Objective

Create the app-owned, backend-neutral Razor boundary, its presentation contracts, isolated tests, project references, and source/dependency guards without migrating existing production consumers yet.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Create the focused app-owned neutral Conversation Components Razor project or the CP1-approved fallback.
- Add source-neutral presentation primitives and the minimum shared infrastructure needed by later extractions.
- Add direct isolated tests and project/solution references.
- Add executable source/dependency/phase guards.
- Do not migrate existing production components yet.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/UI/README.md`
- `src/UI/CanDoItAll.AppComponents/README.md`
- `src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`
- `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
- `tests/Solutions/CanDoItAll.Tests.Components.slnx`
- `docs/architecture/overview.md`

## Required deliverables

- `src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj`
- `source-neutral presentation key/badge/meta records`
- `minimal shared Razor/CSS/markdown infrastructure justified by the later components`
- isolated neutral project tests
- project and repository architecture documentation
- `proof/SB02/dependency-before-after.md`
- `proof/SB02/architecture-change-record.md`
- `proof/SB02/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Run the CP0 prerequisite check and scoped before-change dependency analysis.
2. Use Components MCP library/component recommendations before selecting BaseLib composition.
3. Create the preferred project with the minimal allowed dependency set.
4. Create opaque key and source-neutral presentation primitives without Agent or LlmChats enums/entities.
5. Add only infrastructure that is immediately needed by SB03–SB07; do not create a speculative framework.
6. Add isolated bUnit tests that instantiate the neutral owner without the Agent runtime.
7. Reference the new project from AgentFramework.Components and the component test project, but do not switch production rendering yet.
8. Update architecture/project documentation according to repository conventions.
9. Run actual-diff impacted-test analysis and affected builds/tests.
10. Run source/dependency guards and before/after CodeAnalytics graph.
11. Close CP1 through architecture review.

## Architecture and dependency gate

- Use the narrowest healthy CodeAnalytics snapshot for architecture-relevant changes.
- Keep source-neutral presentation free of AgentFramework, LlmChats, backend, persistence, and runtime dependencies.
- Reject cycles, wrong project-reference direction, service location, partial-class growth, facade-only extraction, and boolean-god components.
- Record what the old owner no longer owns after this subbundle.
- Run `csharp-architecture-review-gate` when this subbundle closes a named architecture checkpoint.

## Impacted-test protocol

For every production change in this subbundle:

1. derive actual diff files and one-based changed line ranges;
2. call `code_analytics_impacted_tests_get` with `behaviorIntent=Unknown`;
3. put inspected-only files in `contextOnlyPaths`;
4. verify healthy workspaces, resolved symbols, and nonzero source/test discovery;
5. run every required selector;
6. promote conditional selectors only when a returned trigger occurs;
7. use `BehaviorPreservingImplementation` only after conservative analysis justifies it;
8. record request, response, selectors, discovery counts, commands, results, containment, and promotion decisions.

## Focused test intent

- Build the new neutral project.
- Build AgentFramework.Components after adding the reference.
- Run required impacted-test selectors with nonzero discovery.
- Run the isolated neutral contract/component tests.
- Do not run Playwright unless the new project unexpectedly changes rendered production output, which should be treated as a scope error.

## Browser/UI proof

- No production rendering should change.
- If any production DOM changes, stop and return them to the later owning subbundle.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] Preferred project or justified fallback exists and compiles.
- [ ] Neutral source and csproj contain no forbidden dependency.
- [ ] Before/after graph proves no cycle.
- [ ] Neutral presentation primitives use opaque keys and no backend entities.
- [ ] Direct tests do not construct the Agent runtime.
- [ ] Existing production Agent rendering is unchanged.
- [ ] CP1 passes.

## Do not do

- Do not reference Modules.LlmChats.
- Do not add DI registrations or runtime services.
- Do not migrate Agent components.
- Do not create a universal conversation service.
- Do not add a new product menu/route.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `CP1` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
