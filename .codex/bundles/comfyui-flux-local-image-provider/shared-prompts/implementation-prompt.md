# Implementation Prompt

Implement one subbundle at a time for `.codex/bundles/comfyui-flux-local-image-provider`.

- Start by reading the root README, phase plan, traceability, original request, and the active subbundle README.
- Respect the hard stop: if SB01 cannot prove live ComfyUI Flux image generation, mark SB01 `Blocked`, update the execution report, and do not change production driver/provider code.
- Keep the production implementation small and aligned with existing provider-runtime boundaries.
- Do not add fallback providers, silent workflow substitutions, or magic string identifiers outside typed constants/options.
- Capture command transcripts under `proof/SBxx/transcripts/` and cite them from `proof/SBxx/manifest.md`.
- For every critical subbundle, create `proof/SBxx/semantic-invariants.md`, `proof/SBxx/manifest.md`, source assertions, anti-stub audit output, and failing-first or explicit non-production exemption evidence.
- Update `reviews/01-execution-report.md` while proof is fresh.
