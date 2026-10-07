# Artifact truth and independent oracles

Native artifact creation, content access and runtime authority remain production code. The
read-only helper `tools/inspect_artifact.py` can inspect exported test copies independently;
it neither writes product data nor validates the authenticity of a test record. It is not a
substitute for the native viewer, owner readback or a real-model analysis.

## Common identity chain

For every deliverable retain: project/lifetime, intended parent, node/record ID, agent run and
session, provider/model and attempt, approval/receipt, storage binding, content revision, exact
length, SHA-256 and browser download. Compare native bytes with downloaded bytes. Sources are
read through permitted APIs/tools, not inferred from titles, Notes, projected paths or a shared
filesystem root. No fallback to Notes when managed content authorization fails.

A file may exist after a later step fails. Observe the original ID before any retry. Preserve
storage-only/attachment-pending outcomes; do not silently create a second deliverable. Rejected
write tests must compare the correct mounted target and an independently verified neighbor.

## Raster images

Use a real decoder as well as MIME/signature checks. Require positive dimensions, valid complete
payload, supported bounded size and a visible image in the actual viewer. A queued placeholder,
header-only blob or fake extension cannot pass. Preserve operation ID through generation, storage,
node binding and subsequent vision read. The analysis call must consume the image bytes, not a
caption placed beside them. Confidence/uncertainty in the real model's answer remains explicit.

## SVG

Check one SVG namespace/root, XML well-formedness, escaped labels, unique IDs and resolved local
references. Reject scripts, event attributes, `foreignObject`, external href/resources, unsafe
CSS imports and data/remote references. Rendering must retain the product's existing safe
policy. XML inspection is not a replacement for browser visual verification, and the simple
helper is a deliberately strict demo oracle rather than a universal SVG sanitizer.

## XLSX

A real `.xlsx` is an OpenXML ZIP workbook, not CSV/HTML with a renamed extension. Require correct
workbook/sheet relationships, meaningful sheet names, actual numeric types and requested formula
text. Inspect representative values and totals with an independent parser/calculation procedure.
Disallow macros, external connections/relationships and formula-error cells in demo fixtures.
The helper reports formula caches but does not calculate them. Do not treat a missing/stale
formula cache as an evaluated number, or silently fill one with the expected answer.

When the native tool only exposes formulas, document that exact limitation and compute the
small expected result independently from the actual source cells. The real analyst must read
the actual cells/formulas and explain its computation. If the customer's intended opening path
requires evaluated caches, run a supported local calculation/inspection engine and verify its
output on a copy; preserve the formula-bearing original. Do not invent support for arbitrary
Excel features or add a new spreadsheet engine during this UI closure.

The post-create mutation must use a real supported native write and produce a new revision.
Verify both original and changed values. An Agent answer repeated from its authoring prompt or
an unrelated generated Summary export cannot satisfy this requirement.

## Coverage and privacy

Use synthetic names, contracts, prices and project data, not customer material. Keep hidden
oracles and raw provider/approval logs private. Commit only redacted results, runnable setup,
small safe fixture definitions and checks. The checked-in bundle carries no secrets or binary
results alleged to come from the application. All test-generated files are isolated by run.
