
# Architectural decisions

## ADR-001 — Use metadata JSON for node-specific state

**Decision:** Persist detailed node-specific fields through a structured metadata payload rather than many new columns.  
**Why:** The note set spans meetings, tasks, files, environments, infrastructure, and Prompt Factory UX. A column-per-field approach will not scale.

## ADR-002 — Add only moderate new object types

**Decision:** Introduce a small number of new node families and express detail through subtype plus metadata.  
**Why:** Broad families remain behaviorally meaningful while detailed variants stay flexible.

## ADR-003 — Reuse existing modules before creating new registries

**Decision:** Reuse Resources, Workspace, Security, LaunchProfileSettingsResolver, and WorkspaceRuntimeProcessTools.  
**Why:** The repo already contains exactly the kinds of abstractions these notes need.

## ADR-004 — Shared floating tool-window host

**Decision:** Build one reusable floating host for toolbox-like panes in both canvas editors.  
**Why:** Prompt Factory and Project Structure both need the same class of floating, pinned, movable tool window.

## ADR-005 — Browser-realistic execution model

**Decision:** “Open terminal” means an app-hosted terminal or runtime session, not a native terminal launch.  
**Why:** Browser apps cannot reliably launch arbitrary local terminals.

## ADR-006 — Confirmation gate for LLM actions

**Decision:** Transcript-related LLM actions require confirmation and provider selection.  
**Why:** The notes explicitly require confirmation and provider choice between OpenAI and Local Ollama.

## ADR-007 — Screenshot evidence is mandatory for UI changes

**Decision:** UI-changing items must produce screenshot evidence and a semantic review note.  
**Why:** The user explicitly requested screenshot-driven validation for the canvas editors.
