# Execution Report — SB02

## Outcome

SB02 passed. Presentation collections now snapshot caller-owned input, opaque keys are trimmed and bounded, the neutral active list renders declared actions, and the Agent adapter preserves its Open/Stop behavior without leaking those semantics into Conversations.Components.

## Minimal Source Changes

- Added one internal immutable collection snapshot helper and three source-neutral active-action value contracts.
- Hardened existing collection-bearing presentation records while retaining positional-record source compatibility and clone/deconstruction behavior.
- Replaced neutral `IsVisible`/`CanStop` and Open/Stop callbacks with declared action descriptors and one opaque action request.
- Moved Agent labels, icons, disabled rules, danger styling, action-key interpretation, and callback dispatch into the Agent adapter/module.
- Normalized `ConversationPresentationKey` by trimming outer whitespace and rejecting values longer than 256 characters without interpreting product identity.

## Validation Selection

- Platform: VSTest under .NET SDK `10.0.303`; xUnit v2.
- Final CodeAnalytics correlation: `code-analytics_375c0ce4d4fd4f35b6f538b123f08b55`.
- Workspace: healthy Components workspace, 113 projects and 926 source tests.
- Required selection: `AllSuppliedSuites`; 994 runtime cases discovered.
- Broad-gate decision: the Components suite was required and run. Unfiltered Stable and Playwright remained forbidden for SB02.

## Commands And Results

- Failing-first primitive selector: 8 cases; 3 intended failures and 5 passes before implementation.
- Focused final selector covering `ConversationPresentationPrimitiveTests`, `ConversationFloatingComponentsTests`, and `AgentActiveChatPresentationMapperTests`: 17/17 passed.
- `dotnet build` for Conversations.Components, AgentFramework.Components, and Modules.AgentFramework: pass, 0 warnings, 0 errors.
- Initial sandboxed full Components attempt: 740 passed and 254 failed only because access to the configured AppData control-plane lock was denied.
- Approved unsandboxed validation of the same selection: 994/994 passed. The definitive final-candidate run passed 994/994 in 10m29s.
- `git diff --check`: pass.
- Neutral active-list semantic grep for Agent/Open/Stop/CanStop: zero matches.

## Behavior Evidence

Positive cases prove immutable snapshots through construction and record cloning, normalized key equality, generic declared-action rendering/routing, and unchanged Agent labels, tones, icons, disabled rules, test-id suffixes, and handle round-trips.

Negative cases reject blank/oversized keys, null collection entries, invalid Agent handle keys, and unknown Agent action keys. Disabled actions do not dispatch.

## Architecture Review

The review initially caught a needless loss of positional-record named-argument and deconstruction shape. The implementation was corrected to keep positional records and enforce snapshotting in explicit `init` accessors, including `with` expressions. No project reference changed. Dependency direction remains Conversations.Components → Agent adapter → Agent module, with no project cycle.

## Risks

- Keys that differed only by outer whitespace now compare equal; this is the required normalization behavior.
- Keys longer than 256 characters now fail explicitly at construction.
- Consumers of `ConversationActiveItemPresentation` must declare actions rather than passing Agent-specific booleans; all repository consumers compile and the full Components suite passes.

## Requirements Closed

`SCUI-006`, `SCUI-007`, `SCUI-008`, `SCUI-009`, the SB02 preservation slice of `SCUI-017`, and `SCUI-062`.

## Progression Decision

Pass SB02 and unlock SB03. Keep CP1, browser activation, and later Simple Chat behavior locked.

