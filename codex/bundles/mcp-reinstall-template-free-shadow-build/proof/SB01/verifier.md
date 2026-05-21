# SB01 Verifier

## Fake-Proof Resistance

- Compile-only proof is insufficient because SB01 requires `bundle://proof/SB01/transcripts/reinstall-pass.txt`, the full script path from the user report.
- Path-shortening-only proof is insufficient because `bundle://proof/SB01/transcripts/artifact-scan.txt` scans current MCP artifacts for copied `Templates` directories.
- Skill-sync regression is checked by the passing reinstall transcript and by the install manifest details recorded in `bundle://proof/SB01/transcripts/artifact-scan.txt`.
- Stub or fixture-only implementation is checked by `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.

## Closure Decision

- SB01 is accepted. The failing symptom was reproduced before the fix, the full reinstall path passed after the fix, current MCP artifacts do not contain copied `Templates`, and the install manifest retained skill-sync metadata.
