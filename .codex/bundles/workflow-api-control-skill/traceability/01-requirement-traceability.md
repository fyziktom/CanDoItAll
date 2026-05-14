# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R001 / N001 | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `subbundles/01-workflow-api-gap-closure` | Workflow API integration tests | API parity review identified lifecycle/import/export as missing commands. |
| R002 / N001 | `requirements/01-normalized-requirements.md` | `subbundles/01-workflow-api-gap-closure` | Compile and invalid-route tests | Keep typed DTOs and predictable errors. |
| R003 / N002 | `architecture/01-target-solution.md` | `subbundles/02-workflow-api-skill-and-reinstall-setup` | Skill file exists and is installed | Follow existing API skill structure. |
| R004 / N003 | `reviews/01-execution-report.md` | `subbundles/02-workflow-api-skill-and-reinstall-setup` | Official OpenAI docs evidence | OpenAI docs MCP unavailable; official web fallback used. |
| R005 / N004-N005 | `reviews/01-execution-report.md` | `subbundles/03-validation-and-environment-setup` | Reinstall script and local skill proof | User still needs to restart Codex before testing live skill selection. |
| R006 / N001-N005 | `reviews/00-bundle-self-review.md`, `reviews/01-execution-report.md` | `subbundles/03-validation-and-environment-setup` | Bundle validators and targeted tests | Final closure only after proof is recorded. |
