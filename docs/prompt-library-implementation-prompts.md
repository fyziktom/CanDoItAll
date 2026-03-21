# Prompt Library Implementation Prompts

## Prompt 1: Seed Import and Catalog Integrity

Implement a deterministic importer for the prompt-library pack located under `output/prompt-library`.
Requirements:
- import all 112 components, 10 flows, and 13 blueprints
- preserve source ids, keys, groups, tags, stack tags, toolbox flags, template tokens, block keys, agent sequences, and recommended relationships
- make the import idempotent so reruns update metadata without duplicating records
- expose grouped catalog read models for the prompt wizard and prompt library UI
- fail verification if imported counts diverge from the source pack

## Prompt 2: Session Customization and Attachments

Extend the prompt factory session model so the wizard can hold prompt-library selections as session state instead of only global block ids.
Requirements:
- support per-session rendered content for tokenized components
- support session-level flow and blueprint selection metadata
- support prompt-session attachments for note, file, image, video, and link items
- persist uploaded files safely and reference them from the session
- include selected components and attachments in generated prompt output

## Prompt 3: Canvas Right-Click Menu

Extend the prompt wizard canvas menu to expose the imported prompt catalog with logical depth.
Requirements:
- root menu branches: Components, Blueprints, Flows, Inputs, plus existing session commands
- Components branch: Core / Delivery / Environment
- Core branch: Session Framing, Mission & Scope, Context Discovery, Guardrails, Output & Handoff
- Delivery branch: Workflow, Architecture, Planning, Implementation, Validation
- Environment branch: Stack Profiles, Toolbox
- Blueprints and flows must also be grouped into layered branches
- adding an item must create a visible subitem node on the canvas immediately

## Prompt 4: Token Modal and File/Image Modal

Use the existing canvas composer model and extend it for prompt-library token prompts.
Requirements:
- render dynamic input fields from the component `templateTokens` array
- prefill sensible defaults where possible
- allow up to at least 5 token fields in one modal
- for file/image/video input actions, reuse the file picker and drag-drop composer behavior already used by the project structure canvas
- confirm the rendered node content uses the filled token values

## Prompt 5: Prompt Library Explorer

Extend the prompt library route so it becomes a library explorer for saved prompt artifacts and the imported prompt pack.
Requirements:
- keep existing prompt draft/final artifact management intact
- add browsable views for components, flows, and blueprints
- show source counts, grouping, summaries, prompt types, recommended relationships, and token requirements
- make it easy to confirm the UI count still matches 112 / 10 / 13

## Prompt 6: Inventory Guard

Add automated checks that reconcile the imported data twice.
Requirements:
- first reconciliation: seed import counts versus source JSON files
- second reconciliation: UI/library counts and screenshot artifact counts versus imported records
- break the build if any component, flow, or blueprint is missing from either pass

## Prompt 7: Screenshot Verification

Build automated Playwright verification for every imported catalog item.
Requirements:
- create one screenshot for each of the 112 prompt components showing the right-click path and the resulting canvas subitem
- create one screenshot for each of the 10 flow templates showing the resulting canvas subitem
- create one screenshot for each of the 13 blueprints showing the resulting canvas subitem
- verify the created node label matches the imported item
- emit a machine-readable verification summary with expected count, actual count, and missing keys if any
