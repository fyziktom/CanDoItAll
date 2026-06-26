# QA Prompt

Review `.codex/bundles/comfyui-flux-local-image-provider` for execution readiness or closure.

- Verify every raw note in `inputs/02-structured-input.md` maps to a requirement, owning subbundle, and proof path.
- Verify SB01 live proof used `ImageGenerationFlux.json` and did not rely on stale images or another provider.
- Verify SB02 driver/provider changes are minimal, strongly typed, and explicit about missing configuration or node failures.
- Verify SB03 proves project-structure asset creation through `IAgentImageGenerationService`, not direct ComfyUI calls in project-structure code.
- Reject closure when proof is prose-only, missing transcripts, missing generated image bytes, or missing project-structure content readback.
- Run prepared or completed bundle validation as appropriate and record failures as reopen work rather than residual-risk prose.
