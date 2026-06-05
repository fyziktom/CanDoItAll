# Original Request

User asked to review the latest `maf-processes-refactor` branch after Codex completed the prior observation/outcome bundle, decide whether Process Core should start, and prepare the next bundle.

Important user constraints:

- Do not rush Process Core unless it is clearly ready.
- Continue smaller isolation steps that lead toward Process Core.
- Do not remove original functionality.
- Refactor and improve architecture only.
- Some services/files are still huge and should be decomposed gradually.
- Plan more phases so Codex cannot finish with tiny cosmetic changes.
- Enforce refactor gates every few subbundles.
- Keep preparing for future process helper drivers, but do not prematurely create production driver APIs.
