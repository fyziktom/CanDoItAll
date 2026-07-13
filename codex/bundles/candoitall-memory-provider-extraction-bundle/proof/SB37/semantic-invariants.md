# SB37 Semantic Invariants

## SB37-INV-01 - Typed Modes And Ordered Bindings

- Expected: Disabled dispatches none; Automatic uses automatic bindings; ExplicitDirective without a directive dispatches none.
- Shallow implementation: UI flags or raw JSON strings interpreted differently by each runtime path.
- Evidence: `bundle://proof/SB37/transcripts/reported-focused-validation.txt` and `bundle://proof/SB37/transcripts/file-hashes.txt`.
- Negative: zero bindings, disabled, no directive, duplicate alias, malformed settings.
- Downstream: SB40 proved the typed settings through the real hosted editor at desktop and narrow viewports.

## SB37-INV-02 - Directives Are Authorized And Sanitized

- Expected: leading mem:<alias> tokens resolve only configured aliases and are removed from provider/model-bound text while attachments/metadata remain.
- Shallow implementation: substring parsing, provider ID injection, or stripping only the provider query.
- Evidence: parser/planner/context hashes and 22/22 + 29/29 focused proof.
- Red-team: quoted/code/malformed/unknown/disallowed aliases receive zero unauthorized calls.
- Downstream: SB40 captured real-seam provider selection and sanitized prompt behavior.

## SB37-INV-03 - Fan-out Has A Dedicated Owner

- Expected: AgentFramework.Memory owns bounded stable-order fan-out and required/optional merge; generic Memory Application remains one-provider.
- Shallow implementation: loop through registry entries inside Application or module UI.
- Evidence: project/reference audit and hashes in `bundle://proof/SB37/transcripts/source-and-anti-stub-audit.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Settings/plan | Models codec/editor | invocation planner | catalog save/load and invocation tests | malformed/duplicate/disabled cases |
| Provider-labelled context | fan-out/merger | MAF context/tool/workflow adapters | focused runtime suites | required/optional failure and unknown alias |

## Validator Invariant Contract

- Invariant ID: SB37-AGENT-MEMORY-ROUTING
- Source raw note: agents may bind multiple memory providers and use Automatic or explicit alias-forced invocation.
- Expected behavior: typed settings persist stable bindings; automatic fan-out is bounded/stable; explicit directives are sanitized and authorized; disabled/no-directive/unknown paths dispatch nothing.
- Disallowed shallow implementation: UI-only flags, querying every registry entry, magic string matching, or leaving directives in provider/model text.
- Failing-first test: failing-first N/A for this process reconstruction because no production pre-change executable transcript was retained; no baseline is fabricated.
- Passing test: bundle://proof/SB37/transcripts/reported-focused-validation.txt and bundle://proof/SB40/transcripts/terminal-validation.txt.
- Changed source files: repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/MemoryDirectiveParser.cs, repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/AgentMemoryInvocationPlanner.cs, and repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentMemorySettingsPanel.razor.
- Production assertions: agent routing lives in AgentFramework.Memory, application operations remain one-provider and agent-agnostic, and provider content is framed untrusted.
- Red-team negative case: quoted/code-literal, duplicate, unknown, unbound, disabled, and explicit-without-directive inputs prove zero unauthorized dispatch.
- Downstream dependency check: bundle://proof/SB40/transcripts/browser-validation.txt and terminal-validation.txt prove the real UI/runtime seam.
