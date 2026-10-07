# Actual UI demonstration rehearsal

Run these on the final published production candidate, not the UI sandbox. Maintain separate
attempt records for deterministic and genuine-model lanes. Every artifact attempt retains
its exact native identity before continuing after a failure. The small synthetic demo project
is deliberately independent of all previous failure fixtures and the user's ordinary app.
Use [ARTIFACT_REQUIREMENTS.md](ARTIFACT_REQUIREMENTS.md) and the current installed tool schemas.

## Common setup and evidence

Create an owned project through Projects UI (for example `Workbench Demo — October 2026`) with
clearly named nodes for Inputs, Deliverables and Execution. Use one least-privileged author
Agent and, where useful, a separate analyst Agent/new session. Assign only required project,
storage, file, image, spreadsheet and execution capabilities. Record intended permissions
and exact native IDs before testing. Do not give All projects/All secrets/global auto-approval.

Create an independent synthetic input under Inputs through a separate native setup action.
Keep its expected text/numbers/visual attributes out of the Agent prompt and conversation.
Store the oracle privately. In each scenario preserve the initial input hash, accepted tool
proposals and final artifact/analysis identities. A new session for analysis must not inherit
the generation conversation or a textual explanation of the answer.

### D01 — contextual read, exact write and denial

Select Inputs, open the contextual Agent and ask it to summarize the actual selected input.
Require a project-structure read and content read, not a guess or a filesystem-wide search.
Ask for a short Markdown deliverable under Deliverables. Review its exact write and attachment
proposals, approve them, then read the native asset back and open/download it through UI.
A separately rejected overwrite or out-of-project request must not change the original or
neighbor canary. Check the actual attempted workspace mount; absence in an unrelated folder
proves nothing. Close/reopen the same saved session and inspect the accepted result.

### D02 — native SVG creation and independent analysis

Ask the Agent for a useful SVG project illustration from the input (for example three labelled
phases and dependencies), with SVG-native geometry and no external resources. Require its
current SVG skill [S21], native creation, XML validation, exact project parent and readback.
Use the real preview, or its supported safe SVG representation; do not weaken sanitization.

Open a fresh analyst session with only the created asset/node selected. Ask for exact labels,
connections and one requested count. Verify `project_structure_asset_text_get` or the supported
asset-content path, compare against an independently parsed SVG, and record the answer. This
is textual/vector analysis, not a claim of raster visual inference. Malformed XML and an
external/script-bearing SVG must remain rejected by deterministic negative tests.

### D03 — real image generation and real vision readback

Ask for one simple raster illustration using an explicitly configured image provider. A
placeholder and queued status are not completion. Wait for the original operation, exact
stored image and independently decodable bytes. Inspect PNG/JPEG/WebP as applicable and open
and download the actual file. No relabelled SVG, synthetic success, or regenerated replacement
because the observer lost track of the first operation.

In a new analyst session select the generated ImageAsset and ask concrete visual questions
that were not supplied as Notes or in the prompt. Require the native
`project_structure_asset_image_analyze` path [S20]. Its real vision provider must receive the
actual authorized image bytes. Independently inspect the image and assess the answer; do not
require nonexistent features from a stochastic generated image. Add a second preexisting
private visual canary with known shapes/colours/counts for objective readback verification.
For deterministic tests use fixture image bytes but label that separate from genuine generation.

### D04 — actual Agent-authored XLSX and new-session calculations

Ask the author Agent to produce a small workbook from an existing synthetic input table.
Use sheets `Inputs`, `Calculations`, `Summary`; include types, units/currency, ordinary A1
formulas, totals, a ratio guarded against division by zero and a readable summary. Use actual
`workspace_write_spreadsheet` and relevant catalog/read tools [S22], then register the real
binary as a project asset. A Progress Summary export is a separate feature, not this test.

Inspect the downloaded ZIP/OpenXML package independently: sheets, values, formulas, relationships
and absence of formula errors. Record which engine, if any, evaluated formulas. Formula text
and cached values are distinct; do not replace formulas with constants to satisfy a check.
Render/open through the supported native viewer or a controlled local spreadsheet inspection
path without sending it to an online viewer. Verify visible headers and representative cells.

In a new analyst session provide only the asset/node and ask for two totals and the largest
cost row, with the cells/formulas supporting the answers. The Agent must use actual spreadsheet
reads, after native asset access supplies any authorized workspace path. Independently change
one test input cell via the native owner, creating a new revision, without telling the Agent
its value. Start another analysis/read and require the changed answer and correct revision.
It must not answer from the old conversation or stale cached metadata. Keep the original and
updated revision/bytes separately. Do not inject a test answer through application internals.

### D05 — saved Workflow from Workbench

Use the product's normal Workflow facilities to prepare a small saved definition with a
published/saved version as required. Link it to the demo project or canonical task using the
new Workbench UI. Review original inputs and selected version, run with simulation disabled,
then verify the same accepted intent/run completes and writes the expected native artifact.
Open that artifact and the authorized History; observe known incomplete output as a separate
negative test, never accepted as success. Closing or reopening UI may not create another run.

### D06 — actual Process, not just preparation

Use a deliberately small supported Process with one or two meaningful steps and a real assigned
Agent. Publish the definition using the existing Processes product, link it through Workbench,
review roles/input/version, Save and close, reopen the saved preparation and Start reviewed run.
Trace prepared admission, accepted run, actual step execution and graph-link delivery. Require
terminal successful completion plus a specific output file or node, native content readback and
visible progress in the existing Process UI. Include one approval/manual response only if the
chosen definition supports it. Do not bypass missing roles by rewriting governance or manually
marking records Completed. A second intentional run must have a different explicit intent.

### D07 — durable restart and adjacent functions

Restart the candidate cleanly twice with the same owned PostgreSQL/storage and accepted data.
Reopen project, files, transcripts, Workflow and Process. A completed enabled finite Scheduler
plan must not prevent startup or redispatch; one future neighbor should execute once using
the existing scheduler test path. Inspect Calendar/Gantt, Summary/Activity, participant/meeting,
secret-reference metadata and runtime preview. Closing preview must not Stop a process; an
explicit Stop targets only its accepted identity. Do not invoke elevation unnecessarily.

### D08 — source/client and final operator walkthrough

Use the existing owned source plus two clients for targeted final checks. Friendly default and
alternate model names match source metadata while opaque route IDs remain exact. An unavailable
source never falls back to an unrelated provider. Check actual Agent/Workflow consumers and
source isolation; repeat the complete protocol only when inputs changed or the final closure
plan calls for it. Record any legitimately inherited unchanged protocol evidence as inherited.

Then execute the customer's intended uninterrupted walkthrough on the final candidate: start,
open project, contextual read, one already-defined creation, independent analysis, Workflow,
Process, files, History and shutdown/reopen. Write down actual buttons, model choices, normal
latencies, known limitations and artifact IDs. Distinguish a prepared-data fallback from a live
run. The fallback can keep the presentation useful but does not make a blocked live lane pass.
