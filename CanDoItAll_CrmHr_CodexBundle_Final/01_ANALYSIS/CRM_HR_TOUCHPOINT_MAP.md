# CRM / HR touchpoint map

| CanDoItAll area | Current capability | Needed CRM/HR addition | Story IDs | Primary subbundles |
| --- | --- | --- | --- | --- |
| Shell navigation and workspace | Route-based shell with Dashboard, Projects, Resources, Prompt Gallery, Prompt Factory, Validation, Test Lab, Activity, Automation, and Settings. | Add CRM / HR navigation entry, nested route support, tab labels, and route-aware context chips. | X-01, DIR-15, CRM-18, AI-08 | B02, B05, B09, B11 |
| Module composition and startup | Assemblies are registered centrally and EF model configurations are discovered from listed module assemblies. | Register the new module assembly, startup seeding, and services without breaking local auto-create behavior. | X-05, X-08, DIR-01 | B01, B09, B11 |
| Projects module | Projects have name, status, phase, options, hierarchy, and dashboard/structure/calendar actions. | Show primary customer, delivery unit, account manager, linked opportunity, and party-based filters on project surfaces. | PRJ-01, PRJ-02, PRJ-09, PRJ-10, PRJ-11, CRM-22, HR-35 | B02, B04, B05, B07, B10 |
| Workbench structure and calendar | Project structure already supports Participant, Meeting, WorkItem, Repository, Script, Environment, Infrastructure, and AI-agent participant kinds inside project-only metadata. | Replace CRM-lite participant isolation with central party references, party pickers, assignment metadata, and optional local-only projections. | PRJ-03, PRJ-04, PRJ-05, PRJ-06, PRJ-07, PRJ-08, PRJ-15, PRJ-16, AI-05 | B10 |
| Resources module | Typed project resources include repository, folder, file, web link, FTP, SSH, script, secret link, and prompt link patterns. | Add owner and maintainer party linkage so operational responsibility is visible across projects and shared resources. | PRJ-13, CRM-21 | B04, B11 |
| Workspace providers and AI execution | Provider profiles capture OpenAI/Ollama connection details, model defaults, health, and capability flags. | Bind AI agent party records to provider profiles, default model strategy, ownership, and review status. | AI-01, AI-02, AI-03, AI-04, AI-06, AI-07, AI-08 | B02, B09 |
| Activity and global search | Relational search index and activity timeline already support cross-entity history and direct route navigation. | Index parties, interactions, opportunities, workforce profiles, candidate flows, and AI agents; write meaningful CRM/HR activity entries. | DIR-14, DIR-15, CRM-19, X-02, X-03, X-11 | B02, B04, B11, B12 |
| Validation center | Validation runs and findings can be attached to projects and routes but not to responsible people or reviewers. | Link responsible owner, reviewer, or approver parties to validation runs so accountability flows through the new module. | PRJ-12, X-03 | B11 |
| Test Lab | Test plans, cases, evidence, and runs attach to projects, not to named owners or reviewers. | Link owners and reviewers to test plans and evidence where accountability matters. | PRJ-12, X-03 | B11 |
| Automation workspace | Automation workspace currently lists tracked background jobs. | Add reminder jobs and job visibility for overdue next actions, onboarding tasks, and expiring contracts or allocations. | CRM-20, HR-24, HR-25, HR-33, X-15 | B04, B07, B08, B11 |
| BaseLib UI components | BaseLib supports layout, tabs, filters, list/detail shells, cards, form sections, badges, and feedback for simple CRUD pages. | Use only BaseLib and normal HTML/Tailwind on all CRM/HR pages; do not import canvas libs. | X-04, DIR-03, CRM-18, HR-18 | B02, B05, B07 |
| Playwright and automated validation | Repo already supports local Playwright smoke tests, screenshots, and runtime bootstrapping with a temp SQLite database. | Add full CRM/HR browser flows, screenshot evidence, and semantic analysis for visual validation and regression confidence. | X-06, X-07, X-16 | B13 |


## Highest-risk touchpoints

1. **Workbench participants and meetings**  
   This is the most sensitive area because the app already stores participant-like data in project-local metadata. The new module must not make existing structure flows worse. The correct move is to keep participant nodes while adding central `PartyId` references and project-assignment synchronization.

2. **Projects list and summary**  
   Enterprise CRM/HR value is weak if projects cannot show customer, delivery unit, partner, and ownership context. This must be addressed explicitly, not left for a future polish pass.

3. **AI agents**  
   AI agents already exist in two partial forms: Workbench participant kind and Workspace provider profile. The CRM/HR module must merge identity and configuration rather than introducing a third AI registry.

4. **Validation / Test Lab / Resources accountability**  
   Once the directory exists, these modules will look incomplete if ownership and responsibility remain anonymous. Cross-module responsibility fields must therefore be part of the design, not an afterthought.
