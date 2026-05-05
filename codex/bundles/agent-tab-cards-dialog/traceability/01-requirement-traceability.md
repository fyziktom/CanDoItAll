# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001/R001 Agents tab layout changes to card-led surface. | `requirements/01-normalized-requirements.md` | `subbundles/02-agents-tab-dialog-editor` | Component test and `/agents?tab=agents` browser screenshot. | Depends on shared card foundation. |
| N002/R002 Same component for switch modal and Agents tab cards. | `architecture/01-target-solution.md` | `subbundles/01-shared-agent-card-foundation` | Source inspection plus switch-dialog component tests. | Critical UI foundation. |
| N003/R003 Double-click opens DialogService modal. | `requirements/01-normalized-requirements.md` | `subbundles/02-agents-tab-dialog-editor` | bUnit double-click test and browser open-dialog proof. | Must prove open state. |
| N004/R004 Technical editor remains editable. | `architecture/01-target-solution.md` | `subbundles/02-agents-tab-dialog-editor` | Existing and updated save/edit tests. | SaveAgentAsync remains canonical. |
| N005/R005 Editor sections move into tabs. | `requirements/01-normalized-requirements.md` | `subbundles/02-agents-tab-dialog-editor` | Component assertions for tab labels and dialog markup. | Uses BaseLib Tabs. |
| N006/R006 Skills/MCP tab shows connected and available assignable capabilities. | `requirements/01-normalized-requirements.md` | `subbundles/02-agents-tab-dialog-editor` | Component test for attached/available capability rows and assign action. | Capability creation may remain follow-up if not compact. |
| N007/R007 Fields use available space. | `analysis/02-assumptions-and-risks.md` | `subbundles/02-agents-tab-dialog-editor` | Screenshot review and class/markup assertions. | Visual proof required. |
| N008/R008 Summary and Instructions full-width/taller. | `requirements/01-normalized-requirements.md` | `subbundles/02-agents-tab-dialog-editor` | Component assertion for field container and browser screenshot. | Identity tab proof. |
