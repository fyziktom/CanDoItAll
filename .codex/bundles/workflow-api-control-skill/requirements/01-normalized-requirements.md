# Normalized Requirements

| Id | Requirement | Success Criteria | Owner |
| --- | --- | --- | --- |
| R001 | Review the workflow API against process-style development controls and add justified missing commands. | Workflow API includes explicit definition lifecycle commands plus import/export and targeted tests prove them. | Subbundle 01 |
| R002 | Preserve strong typing and explicit errors in workflow API changes. | New request/response DTOs use workflow model types; invalid ids or invalid imported payloads return predictable API errors. | Subbundle 01 |
| R003 | Add a repo-managed workflow API skill matching existing API skill structure. | `codex/skills/candoitall-api-workflows/SKILL.md` exists with required frontmatter, access guidance, primary routes, operating rules, and validation guidance. | Subbundle 02 |
| R004 | Validate the skill structure against current OpenAI Codex/GPT-5.5 guidance. | Bundle records official OpenAI docs evidence that `SKILL.md` with `name` and `description` is required, descriptions drive invocation, and GPT-5.5 supports skills. | Subbundle 02 |
| R005 | Ensure the new skill is reinstalled with the MCP reinstall script and setup locally. | `tools/Reinstall-CanDoItAllMcps.ps1` syncs repo-managed skills and `%USERPROFILE%\.codex\skills\candoitall-api-workflows\SKILL.md` exists after running the script. | Subbundle 03 |
| R006 | Keep validation scoped but meaningful. | Targeted workflow API tests pass; bundle prepared/completed validators pass or explicit blockers are recorded. | Subbundle 03 |
