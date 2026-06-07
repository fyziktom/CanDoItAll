# Raw Request

User request, normalized:

Codex has completed the previous bundle on branch `maf-processes-refactor`.
Review whether it is complete, what remains to fix or improve, and prepare the next bundle as a ZIP.

Hard preferences:
- Do not rush `Process Core` unless it is clearly justified.
- Continue progressive isolation steps leading toward `Process Core`.
- Preserve all original functionality. This is refactoring and architecture hardening, not behavior removal.
- Avoid micro-subbundles. Use fewer, broader subbundles that span multiple meaningful isolation areas.
- Plan enough work so Codex can work for several hours.
- Force refactoring / proof gates every few subbundles.
- Keep future helper-driver preparation aligned with the original design discussion, but do not create production driver APIs prematurely.
- No small/medium/mobile/browser proof for runtime/service-only changes.
