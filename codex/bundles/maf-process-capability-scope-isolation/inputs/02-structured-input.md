# Structured Input

## Problem Statement

Common MAF runtime tooling contains development-specific prompt behavior. Processes currently can shape prompts and broad operation access, but they cannot reliably suppress or require specific skills, tools, MCP servers, or runtime tool-provider capabilities for a single process step.

## Target Behavior

- Common workspace image analysis remains generic.
- Development-specific image analysis instructions live in a development tool package or in process-owned scoped instructions.
- A process step can declare scoped capability directives that remove selected skills/tools/MCPs/providers from context assembly.
- A process step can require a capability, equivalent to a forced tool or required instruction carrier, and the run fails if the capability is absent or denied.
- A management-only process step can run an agent that normally has development and project-management skills while suppressing the development skill for that step only.

## Explicit Non-Goals

- Do not remove agent-level default capability configuration.
- Do not make prompt text the sole enforcement mechanism.
- Do not add fallback behavior that silently keeps denied capabilities attached.
- Do not couple process core/template projects directly to MAF implementation classes.
- Do not implement production changes during bundle preparation.

## Architecture Direction

- MAF owns generic runtime capability access and common workspace tools.
- Processes own process-step authoring contracts and scoped instruction intent.
- The AgentFramework process adapter translates process-step scope contracts into MAF runtime capability access rules and metadata.
- Development-specific tools and prompts live outside common MAF, in a dedicated development package or module-owned runtime tool provider.
