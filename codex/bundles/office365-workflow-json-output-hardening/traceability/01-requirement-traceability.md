# Requirement Traceability

| Raw note | Requirement | Bundle evidence | Owning subbundle | Planned proof |
| --- | --- | --- | --- | --- |
| N001 | R1, R2, R3 | `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md` | SB01 | Unit tests, source assertions, anti-stub audit, failing-first/passing transcripts. |
| N002 | R5 | `inputs/02-structured-input.md`, `plan/01-phase-plan.md` | SB02 | Local app/API/browser validation at `http://localhost:5032`, or explicit blocker transcript. |
| N003 | R4, R5 | `analysis/01-current-state.md` | SB01, SB02 | Office365 workflow source assertion and downstream live/API validation. |
| N004 | R5 | `inputs/02-structured-input.md` | SB02 | Real Office365 category workflow run/inspection, or exact auth/runtime blocker. |
