# Normalized Requirements

| Id | Requirement | Acceptance Signal |
| --- | --- | --- |
| R001 | The base process execution prompt must be flexible and not assume coding, .NET, Blazor, or calculator work. | Prompt tests assert the generic prompt excludes calculator/Blazor/dotnet-specific lines for representative coding and non-coding steps. |
| R002 | Generic execution discipline must remain: complete actual work, create required artifacts, inspect inherited artifacts when required, handle failed validation explicitly, and emit `PROCESS_STEP_OUTCOME`. | Existing artifact/outcome tests continue to pass and new prompt tests assert generic rules remain. |
| R003 | .NET-specific implementation guidance must live in specialized default agents/skills, not the base prompt. | Seeded .NET agents include .NET/ASP.NET/test guidance and are assigned relevant .NET capabilities. |
| R004 | JavaScript architecture/development/QA default agents must be added with JS-specific instructions and appropriate capabilities. | Seed catalog tests find template keys, instructions, tags, workload, and capability assignments. |
| R005 | Business strategist, financial strategist, and marketing specialist default agents must be added for non-coding processes. | Seed catalog tests find the agents and verify their instructions avoid coding assumptions. |
| R006 | Default processes must include a non-coding business-plan scenario suitable for testing business strategy handoffs. | Template-pack tests load and project a business-plan process with strategist, finance, marketing, and review handoff steps. |
| R007 | Atomic tests must verify prepared-input artifact-shape behavior before handoff and end-to-end process validation. | Focused tests cover prompt shape, seed catalog, template load/projection, and PostgreSQL process execution. |
| R008 | Real validation must use PostgreSQL for process execution. | Validation log records PostgreSQL availability and targeted process tests running with PostgreSQL profile. |
| R009 | Real-agent validation must be attempted on a real scenario after atomic tests pass. | Execution report records completed real-agent run proof or an explicit credential/runtime blocker after deterministic validations pass. |
