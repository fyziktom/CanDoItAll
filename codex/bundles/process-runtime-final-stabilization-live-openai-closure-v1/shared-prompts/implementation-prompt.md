# Implementation Prompt

Implement this stabilization closure bundle phase by phase.

## Instructions
- Execute SB01 through SB06 in dependency order.
- Use model `5.4-mini` for SB02 live OpenAI smoke and keep the API key value redacted.
- Make source/test changes only when a validation result proves they are needed.
- Keep Process Core generic and do not add execution-capable driver behavior.
- Capture transcripts under `bundle://proof/SBxx/transcripts/`.
- Create `bundle://proof/SBxx/manifest.md` and `bundle://proof/SBxx/semantic-invariants.md` for each completed critical subbundle.
- Update `bundle://reviews/01-execution-report.md` while proof is fresh.
