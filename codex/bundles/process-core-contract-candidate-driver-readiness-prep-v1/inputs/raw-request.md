User asks to review the latest `maf-processes-refactor` implementation after Codex completed
`process-core-readiness-final-isolation-drivers-prep-v1`, then prepare the next implementation-ready
bundle as a zip.

Key raw constraints:
- Do not rush `Process Core` unless it is clearly justified.
- Preserve original functionality; this is refactoring/architecture hardening only.
- Plan fewer, broader, meaningful subbundles instead of micro-subbundles.
- Cover multiple isolation areas that move the system closer to future Process Core and future drivers.
- Keep future driver work as preparation unless production APIs are clearly ready.
- No small/medium/mobile/browser proof for runtime/service-only changes; UI proof is N/A unless UI files change.
