# Repository surface map

| Surface | Route | Owning module | Current purpose | CRM/HR relevance |
| --- | --- | --- | --- | --- |
| Dashboard | / | CanDoItAll.Web | Operational summary and app home. | Indirect: CRM/HR summary cards may later surface here, but root module stays separate. |
| Projects | /projects | CanDoItAll.Modules.Projects | Project registry, phases, hierarchy, structure and calendar entry points. | Direct: project/customer/partner/delivery-unit/assignment integration. |
| Structure canvas | /projects/{ProjectId}/structure | CanDoItAll.Modules.Workbench | Project structure nodes, participant nodes, meetings, work items, and linked metadata. | Direct: central party pickers and participant sync. |
| Project calendar | /projects/{ProjectId}/calendar | CanDoItAll.Modules.Workbench | Calendar rendering of project structure scheduling data. | Direct: meeting participants and owners should come from CRM/HR. |
| Resources | /resources | CanDoItAll.Modules.Resources | Typed project resource registry. | Direct: resource owner and maintainer party links. |
| Prompt Gallery | /prompt-gallery | CanDoItAll.Modules.Prompts | Prompt library and collections. | Indirect: AI agents may eventually reference prompts or galleries. |
| Prompt Factory | /prompt-factory | CanDoItAll.Modules.Factory | Prompt session workbench and assembly flows. | Direct for AI-agent reuse only; no CRM/HR UI here. |
| Validation Center | /validation | CanDoItAll.Modules.Validation | Validation checklists, runs, findings, review decisions. | Direct: responsible parties and reviewers. |
| Test Lab | /test-lab | CanDoItAll.Modules.TestLab | Test plans, evidence, execution records. | Direct: responsible parties and evidence owners. |
| Activity | /activity | CanDoItAll.Modules.Activity | Cross-entity activity and search. | Direct: CRM/HR change history and search. |
| Automation | /automation | CanDoItAll.Modules.Automation | Background job visibility. | Direct: reminders and onboarding/offboarding automation visibility. |
| Settings | /settings | CanDoItAll.Modules.Workspace / Security | Provider profiles, secrets, workspace defaults. | Direct: AI-agent provider binding reuse. |


## Notes

- The new CRM / HR module should be introduced as its own shell surface rather than being hidden inside Projects or Workbench.
- Nested routes under `/crm-hr` will match the shell route pattern already used by `ShellNavigation.IsRouteMatch`.
- Project Structure and Project Calendar are not shell entries on their own; they are subroutes under Projects and must be integrated carefully rather than re-skinned.
