# SB12 Sidecar Drift Report

Observed at: 2026-06-15

## Scanner Scope

The scanner treats generated sidecars as:

- Markdown files under a process root.
- Mermaid files under a process root.
- JSON files under a `projection` directory.

Component JSON files outside `projection` are not treated as generated projection sidecars by this scan.

## Counts

- Generated sidecars scanned: 378
- Markdown sidecars: 285
- Mermaid sidecars: 48
- Projection JSON sidecars: 45
- Projection JSON with `sourceJsonHash`: 0
- Projection JSON missing `sourceJsonHash`: 44
- Projection JSON unreadable: 1

Unreadable projection JSON:

- `Templates/Processes/processes/hotfix-rollout/projection/current-module.import-envelope.json`

## Decision

Sidecars are not canonical source. Missing or unreadable source hash data requires regeneration or manual review. SB12 does not delete, overwrite, or silently accept sidecar content.

## Evidence

- Raw scan: `sidecar-source-hash-scan.txt`
- Canonical-source scan: `markdown-mermaid-canonical-source-scan.txt`
