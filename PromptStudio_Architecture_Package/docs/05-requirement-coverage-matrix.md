# 05 — Requirement Coverage Matrix

This matrix demonstrates that the proposed UX, architecture, implementation plan, and Codex prompts cover the requested scope.

## 1. Coverage legend
- **Docs** — where the requirement is described
- **Modules** — where the requirement lives architecturally
- **UI** — where the user accesses it
- **Prompts** — which Codex prompts implement it
- **Tests** — where it is validated

## 2. Coverage matrix

| Requirement Group | Covered In Docs | Primary Modules | Main UI Surface | Codex Prompts | Test Focus |
|---|---|---|---|---|---|
| OpenAI integration | 02, 04, 09 | Workspace, Factory, Automation | Settings, Prompt Factory | 03, 04, 08, 12 | integration, provider contract |
| Ollama local integration | 02, 04, 09 | Workspace, Factory, Automation | Settings, Prompt Factory | 03, 04, 08, 12 | integration, provider contract |
| Ollama remote integration | 02, 04, 09 | Workspace, Factory, Automation | Settings, Prompt Factory | 03, 04, 08, 12 | integration, provider contract |
| EF Core with SQLite | 02, 04, 07, 09 | Infrastructure | Settings | 01, 03, 12 | integration |
| EF Core with PostgreSQL | 02, 04, 07, 09 | Infrastructure | Settings | 01, 03, 12 | integration |
| `IDbContextFactory` runtime access | 02, 04, 07, 09 | Infrastructure, all data modules | implicit | 01, 03 | unit, integration |
| design-time context factory | 02, 04, 07 | Infrastructure | none | 01, 03 | migration verification |
| in-memory DB/test mode | 02, 04, 09 | Infrastructure, Tests | Settings (optional) | 01, 03, 11 | integration, smoke |
| project creation | 01, 02, 03, 04 | Projects | Projects, Project Overview | 05, 10 | unit, component, e2e |
| project dates and phases | 01, 02, 03, 04 | Projects | Project Overview, Stack Profile | 05, 10 | unit, e2e |
| project status management | 01, 02, 03, 04 | Projects | Project Overview | 05, 10 | unit, e2e |
| folder resource | 01, 02, 03, 04 | Resources | Resources | 06, 10 | component, e2e |
| file resource | 01, 02, 03, 04 | Resources, Automation | Resources | 06, 10 | component, integration |
| web link resource | 01, 02, 03, 04 | Resources | Resources | 06, 10 | component |
| FTP profile resource | 01, 02, 03, 04 | Resources, Security | Resources, Settings | 03, 06 | component, security |
| PowerShell script resource | 01, 02, 03, 04 | Resources, Security, Automation | Resources | 03, 06, 12 | security, approval |
| repository resource | 01, 02, 03, 04 | Resources, Activity | Resources, Project Overview | 06, 12 | integration |
| Docker / Compose resource | 01, 02, 03, 04 | Resources, Automation | Resources | 06, 12 | component, approval |
| SSH profile resource | 01, 02, 03, 04 | Resources, Security | Resources, Settings | 03, 06 | security |
| secret/key resource linkage | 01, 02, 03, 04 | Security, Resources | Settings, Resources | 03, 06 | security |
| prompt as linked object | 01, 02, 03, 04 | Prompts, Resources | Resources, Prompt Gallery | 06, 07 | unit, e2e |
| prompt gallery | 01, 02, 03, 04 | Prompts | Prompt Gallery | 07, 10 | component, e2e |
| prompt collections/galleries | 01, 02, 03, 04 | Prompts | Prompt Gallery | 07, 10 | unit, e2e |
| prompt search/tagging | 01, 02, 03, 04, 09 | Prompts, Activity | Prompt Gallery | 07, 10, 12 | integration |
| prompt usage history | 01, 02, 03, 04 | Prompts, Activity | Prompt Gallery, Activity | 07, 12 | unit, integration |
| repo/commit linkage in usage | 01, 02, 03, 04 | Prompts, Activity, Resources | Prompt Detail | 06, 07, 12 | integration |
| automatic prompt builder | 01, 02, 03, 04 | Factory | Prompt Factory | 08, 10 | unit, component, e2e |
| save prompt templates | 01, 02, 03, 04 | Prompts, Factory | Prompt Gallery, Prompt Factory | 07, 08 | unit |
| save prompt drafts | 01, 02, 03, 04 | Prompts, Factory | Prompt Factory | 07, 08 | unit, e2e |
| phase-based wizard | 01, 02, 03, 04 | Factory, Projects | Prompt Factory | 08, 10 | component, e2e |
| user stories/use cases definition | 01 | Validation (consumes), docs | Validation Center | 09 | review/manual |
| validation of user stories/use cases | 01, 02, 03, 04, 09 | Validation | Validation Center | 09 | unit, review |
| ASCII layout design | 03 | Validation consumes, docs | Validation Center | 09 | review/manual |
| layout validation vs stories/use cases | 01, 03, 04, 09 | Validation | Validation Center | 09 | review/manual |
| reusable component library plan | 03, 04, 07 | ComponentKit | all screens | 02, 10 | component tests |
| internal tab workspace | 01, 02, 03, 03a, 04, 07 | Workbench, ComponentKit, Web | shell-wide | 01, 02, 06a, 10, 12 | component, e2e |
| tab restore and sleeping lifecycle | 01, 02, 03a, 04, 07, 09 | Workbench, Infrastructure, Web | shell-wide | 02, 06a, 10, 11, 12 | integration, component, e2e |
| project structure canvas | 01, 02, 03, 03a, 04, 07 | Workbench, Projects, ComponentKit | Project Structure | 06a, 10, 11 | component, integration, e2e |
| prompt-step branching on structure canvas | 01, 02, 03a, 04, 07 | Workbench, Factory, Prompts | Project Structure, Prompt Factory | 06a, 08, 10, 11 | unit, component, e2e |
| project events calendar | 01, 02, 03, 03a, 04, 07 | Workbench, Projects, ComponentKit | Project Calendar | 06a, 10, 11 | component, integration, e2e |
| UI architecture | 03, 04 | Web, ComponentKit, modules | all screens | 10 | component, e2e |
| UI implementation plan | 03, 07 | Web, ComponentKit | all screens | 10 | component, e2e |
| UI testing, Playwright, screenshots | 03, 09 | TestLab | Test Lab | 11 | e2e, evidence |
| initial architecture prompt flow | 01, 02, 04, 07 | Factory, Prompts | Prompt Factory | 08 | unit, e2e |
| architecture review flow | 01, 02, 04, 09 | Validation, Prompts | Validation Center | 09 | review, e2e |
| implementation planning flow | 01, 02, 04, 07 | Factory, Projects | Prompt Factory, Project Plan | 08 | unit, e2e |
| plan review/validation | 01, 02, 04, 09 | Validation | Validation Center | 09 | review |
| first implementation workflow | 01, 02, 04, 07 | Projects, Factory, Prompts | Project Plan, Prompt Factory | 05, 08 | e2e |
| prototype validation vs plan | 01, 02, 04, 09 | Validation, TestLab | Validation Center | 09, 11 | review, e2e |
| architecture revision flow | 01, 02, 04 | Validation, Prompts, Factory | Validation Center, Prompt Factory | 08, 09 | review |
| test coverage planning | 01, 02, 04, 09 | Validation, TestLab | Test Lab, Validation Center | 09, 11 | unit, e2e |
| add tests and validate | 01, 02, 04, 09 | TestLab, Validation | Test Lab | 11 | e2e |
| feature mini-cycle | 01, 02, 04, 07 | Projects, Factory, Validation, TestLab | Project Overview, Prompt Factory | 05, 08, 09, 11 | e2e |
| language selection with notes | 01, 02, 03, 04 | Projects | Stack Profile | 05, 10 | component, e2e |
| DB/UI framework selection with notes | 01, 02, 03, 04 | Projects | Stack Profile | 05, 10 | component |
| external API selection | 01, 02, 03, 04 | Projects, Workspace | Stack Profile | 05, 10 | component |
| storage choice with notes | 01, 02, 03, 04 | Projects, Resources, Infrastructure | Stack Profile, Resources | 05, 06 | unit, component |
| generalization of project options | 01, 02, 04 | Workspace, Projects | Stack Profile | 05 | unit |
| prompt output for Codex or other LLM | 01, 02, 04, 07 | Factory, Prompts | Prompt Factory | 08 | unit, e2e |
| modular growth | 02, 04, 06, 10 | all modules | shell-wide | 01, 02, 12 | architecture review |
| future microservices | 02, 04, 06, 10 | Automation, module contracts | settings/admin later | 12 | architecture review |
| unified UI with future expansion | 01, 03, 04 | Web, ComponentKit | shell-wide | 10 | component, e2e |

## 3. Gap analysis summary from the matrix

### No uncovered mandatory requirement
Every mandatory requested capability maps to:
- at least one document,
- at least one owning module,
- at least one UI surface,
- at least one implementation prompt,
- and at least one validation path.

### Known “broad” implementation areas
The following areas are intentionally broad because they require pluggable design:
- resource previews for many file formats
- connector validation implementations
- future sidecar orchestration
- optional advanced search modes

These are covered architecturally without forcing premature over-engineering.

## 4. Critical coverage observations

1. The largest complexity concentration is in **Resources + Security + Factory**.
2. The highest future-growth pressure is in **Automation + Search + Sidecar extraction**.
3. The highest product-value concentration is in **Projects + Prompts + Factory + Validation**.
4. The highest safety pressure is in **Security + provider send/export flows + execution gates**.

## 5. Coverage verdict

Coverage is sufficient for implementation handoff.
