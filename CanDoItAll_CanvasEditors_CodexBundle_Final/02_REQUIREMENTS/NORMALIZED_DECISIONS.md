
# Normalized decisions for ambiguous notes

These decisions intentionally remove ambiguity so Codex can implement consistently.

## 1. Rich metadata instead of schema explosion

The requested node families are too varied for a many-column schema. Add a structured metadata payload such as `MetadataJson` (or a strongly typed equivalent persisted as JSON) and use typed DTOs.

## 2. Moderate `ProjectObjectType` expansion

Add only a small number of new node families at the enum level. Prefer subtype plus metadata for specific variants.

## 3. CRM-lite, not full CRM

The people requirement is satisfied by a lightweight participant registry and reusable selectors. Do **not** build a sales CRM.

## 4. “Open terminal” means an in-app terminal surface

Do not assume the browser can launch a native terminal application. Use a manager-backed or app-hosted terminal/session view rooted to the working directory.

## 5. “OpenFolder dialog” requires a fallback

Use browser directory selection if available, but always provide a manual path or linked-resource fallback.

## 6. Progress versus priority click ambiguity is normalized

The note saying “Left click on Progress icon in node must show only selector of priority” is internally inconsistent. Normalize it as:

- progress badge left-click => progress selector only
- priority badge left-click => priority selector only
- markers remain separate

## 7. LLM-backed transcript actions always require confirmation

Any action that sends content to OpenAI or Ollama must show a confirmation step and provider selection.

## 8. Prompt Factory toolbox redesign must reuse shared floating host

Do not implement separate floating panel systems for Prompt Factory and Project Structure. Build one shared tool-window host first.

## 9. The intermittent 44-node bug needs root-cause evidence

No task closure is allowed without explaining what caused the multi-add behavior and proving the fix with regression coverage.

## 10. Screenshot evidence is a release gate

Passing tests alone are not sufficient for UI-changing items. Every canvas-visible item must include screenshot evidence and a short semantic review.
