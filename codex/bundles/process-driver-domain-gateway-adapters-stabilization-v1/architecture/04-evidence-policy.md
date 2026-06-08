# Evidence Policy

## Required invariants
- Every supplied content payload must have a matching SHA-256 hash.
- URI schemes must be allow-listed and must represent already-supplied evidence, not a path to be resolved by the driver.
- Content size must be bounded.
- Content type must match lane expectations.
- Evidence references and audit facts must carry typed lane/evidence family data.
- Redaction must apply to diagnostics and audit summaries, not just response text.

## Approved URI families
- `bundle://...`
- `process://...`
- `artifact://...`
- `repo://tests/...`
- `repo://codex/bundles/...`

## Denied
- Local absolute paths.
- Relative workspace paths.
- HTTP/HTTPS sources.
- Graph/Office connector identifiers.
- Secret-bearing connection strings.
