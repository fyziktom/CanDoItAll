# Target Solution

## Implementation Shape

- Add `DotnetRun` to the workspace command execution contract and implement it through `WorkspaceCommandPlanBuilder`.
- Expose `workspace_dotnet_run` in the MAF workspace plugin and built-in tool capability switch.
- Seed `workspace-dotnet-run` as a built-in validation tool and assign it to programming, .NET developer, Blazor developer, and QA agents.
- Add a generic `.NET App Delivery` inline skill for scaffold/build/test/run workflows across .NET app types.
- Keep `blazor-ssr-delivery` Blazor-specific but remove sample-topic app examples and route proof to generic interaction/state evidence.
- Add a dedicated `Blazor Application Developer` managed default agent.

## Boundaries

- Do not place .NET or Blazor implementation rules in base process prompts used for non-coding work.
- Do not generate helper code specific to the validation app topics.
- Do not modify generated validation apps manually during subbundle 04; only observe and repair generic platform guidance if needed.
