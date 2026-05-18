# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 | `requirements/01-normalized-requirements.md` | `subbundles/01-tooltip-delay-coverage` | Playwright delayed hover assertion and screenshot | Uses the existing two-second opened-work delay as the baseline. |
| N001 / R002 | `requirements/01-normalized-requirements.md` | `subbundles/01-tooltip-delay-coverage` | DOM checks for absent trigger tooltips | `More`, `Opened`, and `Switch Database` remain popup triggers without tooltips. |
| N002 / R003 | `requirements/01-normalized-requirements.md` | `subbundles/02-module-navigation-contributions` | Targeted navigation composition tests | Shared-kernel contract lets any module contribute future subpages. |
| N002 / R004 | `requirements/01-normalized-requirements.md` | `subbundles/02-module-navigation-contributions` | Unit test plus desktop screenshot showing `Agents` then `Workflows` | AgentFramework contributes the first concrete subpage. |
| N003 / R005 | `requirements/01-normalized-requirements.md` | `subbundles/02-module-navigation-contributions` | Code review and test fixture metadata | Metadata is present for future subitem styling, but current rendering stays flat. |
