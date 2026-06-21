# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001`, `R001`, `R002`, `R003` | `bundle://requirements/01-normalized-requirements.md` | `bundle://subbundles/01-long-simple-note-persistence-contract` | Component test transcript; browser runtime/persisted-state transcript; `proof/SB01/manifest.md` | Critical foundation. |
| `N002`, `R004` | `bundle://requirements/01-normalized-requirements.md` | `bundle://subbundles/01-long-simple-note-persistence-contract` | Adversarial long note test with multiline/punctuation/long-token text; anti-stub audit | Must not close from a short happy-path note. |
| `N003`, `R005`, `R006` | `bundle://requirements/01-normalized-requirements.md` | `bundle://subbundles/02-simple-note-canvas-space-use` | DOM metric proof and screenshot review under `proof/SB02/browser/` | Depends on `SB01`. |
| `N004` | `bundle://inputs/01-canvas-reference.png` and `bundle://reviews/01-execution-report.md` | `bundle://subbundles/02-simple-note-canvas-space-use` | Before/after screenshot references and visual review answers | Screenshot proof is required, not optional. |
| `R007` | `bundle://architecture/01-target-solution.md` | `bundle://subbundles/02-simple-note-canvas-space-use` | Package hash/version proof; build/test transcript using updated package | Required only if CanvasLib runtime assets are changed. |
