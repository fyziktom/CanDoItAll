# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001-R004 | `requirements/01-normalized-requirements.md` | `subbundles/01-01-template-vocabulary-and-ui-option-parity` | Component tests, template vocabulary audit, build, browser proof if server starts | Critical foundation for reload. |
| N002 / R005-R006 | `requirements/01-normalized-requirements.md` | `subbundles/02-02-process-only-development-db-reset-and-template-reload` | SQL before/after transcript, template reload transcript, post-reload counts | Destructive process-only operation. |
| N003 / R007 | `requirements/01-normalized-requirements.md` | `subbundles/02-02-process-only-development-db-reset-and-template-reload` | Representative non-process count preservation transcript and no file deletion proof | Hard constraint; no exceptions planned. |
