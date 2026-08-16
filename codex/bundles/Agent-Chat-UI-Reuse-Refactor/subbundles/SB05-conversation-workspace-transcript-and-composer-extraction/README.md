# SB05 — Conversation workspace, transcript, and composer extraction

## Status

`completed`

## Proof tier

`Governed`

## Dependency

- Depends on: SB04
- Closure checkpoint: CP2
- Owned requirements: UIR-040, UIR-041, UIR-042, UIR-043, UIR-044, UIR-045, UIR-046, UIR-073, UIR-075, UIR-077, UIR-078

## Objective

Extract safe markdown, message/transcript presentation, composer chrome, and extension slots while retaining execution, approval, voice, attachment, prompt-gallery, and backend behavior in the legacy agent facade.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Extract neutral workspace header, transcript, message bubble, safe markdown renderer, composer, and prompt text area.
- Keep ChatWorkspacePanel as an Agent-facing facade that composes agent-only execution/approval/voice/attachment/prompt/runtime slots.
- Preserve every existing Agent workspace callback and interaction.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatMarkdownRenderer.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ChatPromptTextArea.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `tests/Components/CanDoItAll.Tests.Components/ChatWorkspacePanelTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentChatPanelResponsivenessTests.cs`

## Required deliverables

- `neutral message/transcript/header/composer presentation contracts`
- `neutral workspace/message/transcript/composer/prompt/markdown components`
- `focused Agent message/context/workspace presentation adapters`
- legacy ChatWorkspacePanel facade with agent-only slots
- direct neutral component tests
- Agent facade regression tests
- CP2 browser parity evidence
- `proof/SB05/architecture-change-record.md`
- `proof/SB05/manifest.json`

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Inspect the current ChatWorkspacePanel public parameters, callbacks, CSS, test ids, scroll/focus behavior, hidden-context parsing, message roles, execution, approvals, composer, attachments, voice, prompt gallery, and runtime dialogs.
2. Use Components MCP before selecting workspace/card/stack/dialog composition.
3. Define explicit message presentation; keep UserRequestMarker parsing in an Agent adapter.
4. Move or delegate safe markdown rendering with HTML disabled.
5. Extract prompt text area and composer presentation while preserving input/change behavior and attributes.
6. Compose execution, approvals, voice, attachments, prompt gallery, runtime details, and extra actions through focused Agent-owned fragments/components.
7. Do not add new partial files to ChatWorkspacePanel or AgentChatPanel.
8. Ensure parameter updates re-render transcript content correctly without introducing SSE code.
9. Preserve current public Agent facade contract until all consumers migrate.
10. Run actual-diff impacted-test analysis, owner tests, source guards, and CP2 focused browser proof.
11. Run architecture review and close CP2.

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

- Safe markdown with HTML disabled.
- User, assistant, and system/other role presentation as currently supported.
- Explicit hidden-context display/copy mapping from the Agent adapter.
- Timestamps, token metadata, copy action, empty states, errors, selected/busy/disabled states.
- Composer draft, input/change, keyboard, send disabled, send callback, validation/status.
- Agent execution, approvals, cancellation, voice, attachments, prompt gallery, and runtime slots in their current states.
- Every required impacted selector with expected nonzero discovery.

## Browser/UI proof

- Mandatory CP2 focused desktop proof.
- Inspect empty and populated transcript, long markdown, composer, busy/send-disabled state, approval/execution state, open runtime detail or relevant overlay, focus, auto-scroll, and clipping.
- Compare against SB01 and record intentional internal-only DOM deltas.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] Neutral workspace has no Agent/LlmChats/backend dependency.
- [ ] ChatWorkspacePanel remains an Agent facade, not a duplicated backend.
- [ ] Hidden context is parsed outside the neutral project.
- [ ] Markdown remains safe.
- [ ] All existing callbacks and agent-only features remain.
- [ ] No new partial-class growth.
- [ ] Focused tests and browser parity pass.
- [ ] CP2 passes.

## Do not do

- Do not add SSE/polling/API clients.
- Do not move send/cancel/approval commands into neutral UI.
- Do not remove voice/attachments/prompt/runtime behavior.
- Do not cache transcript content in a way that blocks parameter updates.
- Do not create one universal workspace with a broad boolean matrix.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `CP2` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
