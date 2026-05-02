# 03 Generic Agent And Blazor Specialist Seeds

## Status

- Status: `Completed`

## Objective

Update active seeded instructions and catalog entries so generic .NET app delivery is covered and a dedicated Blazor Application Developer agent is available with component-library-first guidance.

## Covered Inputs

- User request to improve default agents, instructions, skills, and tools.
- User request for a specialized Blazor app-building agent.
- User requirement that skills also remain generic rather than calculator/converter-shaped.

## Prerequisites

- Subbundle 02 has exposed `workspace_dotnet_run`.
- Inventory has identified active sample-specific instruction text.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\manifest.json
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\dotnet-application-developer.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\programming-workspace-analyst.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\delivery-qa-observer.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\skills\blazor-ssr-delivery.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedNormalizer.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs

## Deliverables

- Generic `.NET App Delivery` inline skill if needed.
- Updated Blazor SSR delivery skill with sample-topic language removed.
- New `Blazor Application Developer` seed agent.
- Seeded `workspace-dotnet-run` capability assignments.
- Managed seed refresh version bump and tests.

## Dependency Impact

- Subbundle 04 depends on refreshed running-web-app seed data.
- Default process runs become more reliable because agents receive tool and skill capabilities instead of ad hoc helpers.

## Validation Depth

- Seed integration tests for agent, skill, capability, and refresh behavior.
- Source scan over active seed assets for sample-topic terms.
- Build after test changes.

## Implementation Steps

- Add or update generic .NET delivery skill text for scaffold/build/test/run.
- Remove converter/calculator/unit-specific examples from active Blazor/QA instructions.
- Add Blazor specialist instruction asset and manifest entry.
- Add the Blazor specialist to seed builder, managed refresh keys, and agent list.
- Assign run/build/test/new/component/Playwright capabilities to appropriate agents.
- Update integration tests to assert the generic tool and Blazor specialist catalog state.

## Scope Exceptions

- Legitimate Blazor-specific hosting rules may remain in Blazor-specific skills and agents.
- Historical non-active fixture strings may remain when tests intentionally model old app names and do not seed agent guidance.

## Do Not Do

- Do not put Blazor-specific rules into universal process prompts.
- Do not add a validation-app-specific helper.
- Do not weaken component-library-first guidance for Blazor UI work.

## Acceptance Checklist

- Blazor specialist is present in the default managed agent catalog.
- Generic .NET app delivery skill/guidance is present.
- `workspace-dotnet-run` is assigned to relevant programming and QA agents.
- Active seed assets no longer contain sample-topic requirements.

## Proof Required

- Integration test output.
- Source scan output.
- Bundle execution report updated with changed files and validation.

## Browser Validation Logging

- N/A for seed changes. Browser proof occurs in subbundle 04 after the web app is rebuilt/restarted.

## Progression Gate

- Live validation may start only after tests prove the refreshed seed catalog contains the generic run tool and Blazor specialist.

## Suggested Agent Prompt

Update seed instructions, inline skills, manifest, seed builder, normalizer, and tests so .NET app delivery is generic and a Blazor specialist is available. Remove sample-topic app hardcoding from active guidance while preserving real Blazor framework rules.

