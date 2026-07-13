# SB03 Proof Manifest

## Scope

- Subbundle: `SB03-agent-git-skill-and-capability-guidance`
- Status: `Closed`
- Closure date: `2026-06-29`

## Changed Files

| File | SHA-256 |
| --- | --- |
| `Templates/Capabilities/tools.json` | `1F6801278CB5736EA24D59C3D7AB8F8E052BC3FA995EFFC76C1D024FF9B5806C` |
| `Templates/Capabilities/skills.json` | `08906291E81B6EE2D91D09DD368B5B69B5C7B2A5EACB0EAB7955E630ACC23C2A` |
| `Templates/Capabilities/skills/instructions/git-standard-operations.md` | `7FF4744F90B596FCA62BDF7193857FC43D9F93FD2A120A614D3ADE8AC49E2D75` |
| `Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/skills.json` | `E221B7292C704C239C0CCDA05A45CFB65AA67E6C76B7A8D02149264762E9E773` |
| `Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/skills.json` | `E221B7292C704C239C0CCDA05A45CFB65AA67E6C76B7A8D02149264762E9E773` |
| `Templates/Agents/teams/dotnet-delivery/members/dotnet-solution-architect/skills.json` | `CF065CB54EC38449B204936EAB798B194FC32CB15DA5084B4A8D8441FBCFAE8C` |
| `Templates/Agents/teams/javascript-delivery/members/javascript-application-developer/skills.json` | `98C0F8D2C19BE87591F2EAF20F90C408A9E426D23F396A28717DCD4A85CD9807` |
| `Templates/Agents/teams/javascript-delivery/members/javascript-solution-architect/skills.json` | `FD53EFEEA0F621A2437A313783F253F175F06D52D4DAA561E299B612AC96DC1E` |
| `Templates/Agents/teams/delivery-platform/members/programming-workspace-analyst/skills.json` | `2EF4A5227701FE434FDE93F2CDB4F23EDB5876E0C9D379B3865EA18295CF3D62` |
| `Templates/Agents/teams/delivery-platform/members/portfolio-architect/skills.json` | `B426B2ACD067164DFC6EFFD58D586F79675965999036729991F1A3AEE0159C14` |
| `Templates/Agents/teams/delivery-platform/members/security-reviewer/skills.json` | `37BEF0A6193E5551348E5EA8F5FFAB1F22F040CA5997F6E62DD0636444C2341D` |
| `Templates/Agents/teams/business-and-research/members/research-deep-dive-analyst/skills.json` | `CE674849DD1E180C9C8C64CD516EE9608146C19DFA642AE2A17874E140D4B75F` |
| `tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs` | `B1173E400395815534C7AB779484265BE41754084378844465B1143DC8897026` |

## Commands

| Command | Transcript | Result |
| --- | --- | --- |
| JSON syntax validation with `ConvertFrom-Json` for changed template files | Recorded in session output before manifest creation | Passed for every changed JSON file. |
| `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter CapabilityTemplateSeedMaterializationTests --no-restore -p:BuildProjectReferences=false` | `proof/SB03/transcripts/capability-template-focused-tests.txt` | Passed, 9 tests. |
| `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter AgentFrameworkWorkspaceSeedIntegrationTests --no-build --no-restore` | `proof/SB03/transcripts/seed-integration-no-build-tests.txt` | Passed, 26 tests. |

## Semantic Adequacy

- Template materialization proof: unit tests validate the catalog count, inline skill asset resolution, and agent assignment validator.
- Skill/tool contract proof: unit tests assert `git-standard-operations` names every shipped git workspace tool and does not name unavailable workspace git tool names.
- Runtime seed proof: no-build integration tests read the changed template assets and passed the seed integration suite.
- Scope proof: software-development agents receive the mutation git tools; architecture, security, and research roles receive read git tools only.
- Anti-stub audit: `proof/SB03/anti-stub-audit.txt` has no TODO, placeholder, stub, or `NotImplementedException` matches.

## Closure Decision

SB03 is closed. Template-backed agents now have the new git tool descriptors and a complementary inline skill that teaches only shipped local git workspace operations.
