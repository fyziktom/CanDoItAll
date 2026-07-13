# SB03-agent-git-skill-and-capability-guidance

## Status

- `Completed`

## Objective

Add template-backed tool descriptors and an inline git operations skill so app-managed agents know how to use the shipped git tools.

## Success Criteria

- `Templates/Capabilities/tools.json` declares every shipped git tool.
- `Templates/Capabilities/skills.json` declares a `git-standard-operations` inline skill.
- Skill instructions mention only shipped tools and give safe standard-operation workflow guidance.
- Default agents that already receive git tools receive the skill and matching tool assignments.

## Covered Inputs

- REQ-005
- REQ-007

## Prerequisites

- SB02 closure gate passed.
- `bundle://proof/SB02/source-assertions.md` lists final tool names.

## Exact Source References

- `repo://Templates/Capabilities/tools.json`
- `repo://Templates/Capabilities/skills.json`
- `repo://Templates/Capabilities/skills/instructions/repository-playbook.md`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/skills.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/skills.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-solution-architect/skills.json`
- `repo://Templates/Agents/teams/javascript-delivery/members/javascript-application-developer/skills.json`
- `repo://Templates/Agents/teams/javascript-delivery/members/javascript-solution-architect/skills.json`
- `repo://Templates/Agents/teams/delivery-platform/members/programming-workspace-analyst/skills.json`
- `repo://Templates/Agents/teams/delivery-platform/members/portfolio-architect/skills.json`
- `repo://Templates/Agents/teams/delivery-platform/members/security-reviewer/skills.json`
- `repo://Templates/Agents/teams/business-and-research/members/research-deep-dive-analyst/skills.json`
- `repo://tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`

## Deliverables

- Tool capability descriptors for SB02 git tools.
- New inline skill instructions for standard git operations.
- Relevant default-agent assignments updated.
- Template materialization and assignment tests updated.

## Dependency Impact

- SB04 depends on catalog and skill consistency.
- If this subbundle names tools not shipped by SB02, app agents will receive unusable guidance.

## Validation Depth

- Catalog and skill consistency validation.
- Requires source assertions and negative grep proof for excluded remote/destructive tool names.

## Implementation Steps

1. Add tool descriptors to `Templates/Capabilities/tools.json`.
2. Add `git-standard-operations` to `Templates/Capabilities/skills.json`.
3. Create `Templates/Capabilities/skills/instructions/git-standard-operations.md`.
4. Assign the skill and matching tools to default agents that already receive git status/diff where role-appropriate.
5. Update expected capability tests and any integration assertions affected by new assignments.
6. Capture source assertions and command transcripts under `proof/SB03/`.

## Scope Exceptions

- Do not add the git skill to agents that do not receive git tools.
- Do not instruct agents to use push, pull, fetch, reset, checkout, rebase, clean, or force operations.

## Do Not Do

- Do not create a `.codex/skills` operator-only skill as the primary deliverable.
- Do not add skill instructions that rely on unavailable tools.
- Do not weaken capability assignment validation.

## Acceptance Checklist

- Capability template loader materializes the new skill and tools.
- Assignment validator passes for default agents.
- Skill guidance names only shipped tools.
- Agents with git mutation tools have role/access settings that can use them.

## Proof Required

- Focused command transcript for capability template and assignment tests.
- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/source-assertions.md`
- `bundle://proof/SB03/anti-stub-audit.txt`

## Browser Validation Logging

- N/A - no browser-visible or host-visible UI behavior.

## Progression Gate

- SB04 may start only after template materialization and assignment validation pass.
- Source assertions must prove skill instructions match shipped tool names.

## Suggested Agent Prompt

```text
Implement SB03 only. Add template-backed git tool descriptors and an inline skill, assign them to appropriate default agents, update tests, and prove the skill names only tools shipped by SB02. Stop before final closure.
```
