
# Target architecture

## 1. Domain model strategy

Use a three-layer node identity model:

1. `ProjectObjectType` for broad behavioral families.
2. `ObjectSubtype` for concrete variants inside those families.
3. Typed metadata DTOs persisted through a structured JSON payload for detailed fields.

This avoids schema explosion while still allowing strong editor behavior and validation.

## 2. Reuse-first integration strategy

- Reuse `CanDoItAll.Modules.Resources` for repositories, scripts, SSH, Docker-related references, secret links, and prompt links.
- Reuse `CanDoItAll.Modules.Workspace` provider abstractions for LLM-backed actions.
- Reuse manager helpers for .NET launch profile parsing, runtime commands, and execution orchestration.
- Reuse the existing floating inspector infrastructure as the seed for a shared floating tool-window host.

## 3. Canvas editor UX strategy

Build a **shared floating tool-window host** with these capabilities:

- show or hide
- pin
- drag or move
- bounds clamp to visible canvas
- internal vertical scroll
- search header
- content slot for tree views or grouped lists
- optional side preview content

Use that shared host for:

- Prompt Factory components toolbox
- Project Structure standard blocks toolbox

## 4. Node decomposition strategy

Avoid giant mega-forms by decomposing infrastructure and workflow concepts into readable subtrees:

- infrastructure root
  - domains
  - DNS records
  - Docker or proxy
  - database
  - deployment folder
  - keys
  - AI links

Likewise, model meetings, recordings, transcripts, tasks, attachments, and environments as explicit nodes rather than hidden blobs.

## 5. Execution strategy

Interpret execution-related notes through a realistic web-app model:

- script and runtime nodes launch into an in-app terminal or managed session
- launch profiles and command templates are explicit and inspectable
- long-running tasks such as dotnet watch or Tailwind watch reuse the same execution infrastructure

## 6. Validation strategy

Validation must be layered:

- unit tests for parsing, command construction, and metadata round-trip
- component tests for editor behavior and card rendering
- integration tests for persistence and service workflows
- Playwright for canvas-visible end-to-end and screenshot evidence
- semantic screenshot review for all UI-changing items
