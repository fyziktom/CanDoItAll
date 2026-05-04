# Live Process Tool-Profile Regression

## Source

- Date observed: `2026-05-03`
- Project: `80b84f2c-8d8c-4f3c-8016-e7e05cccf2e1`
- Process definition: `4fdc77a9-6d8c-4b10-9efb-4be15732b1b0`
- Run: `cf086486-2424-487b-bd29-bfc3c111f307`
- URL: `https://localhost:7271/projects/80b84f2c-8d8c-4f3c-8016-e7e05cccf2e1/processes?processId=4fdc77a9-6d8c-4b10-9efb-4be15732b1b0&runId=cf086486-2424-487b-bd29-bfc3c111f307`

## Raw Feedback

The process now launches and assigns HR resources, but it blocks on the implementation step with a governed rerun packet. The modal reports that the Blazor implementation agent wrote part of the app and durable artifacts but could not complete validation because the test project was created at an invalid grounded path.

## Reproduction Evidence

- Step `Implement feature, tests, and migration notes` was assigned to `Blazor Application Developer`.
- Runtime logs show `workspace_dotnet_new` failed with `This agent is not allowed to scaffold workspace projects.`
- Runtime logs show `workspace_dotnet_build` failed with `This agent is not allowed to run workspace validation commands.`
- The generated app under `C:\programovani\dotnet\output` also did not compile when checked manually, so the agent needed build/test tools to repair the output instead of improvising from incomplete evidence.

## Failure Interpretation

- HR matching is no longer the primary blocker.
- The implementation agent reached the process step with a tool surface that did not match the runtime permission checks.
- The old bundle closure was too weak because it did not prove process-scoped workspace tool-profile overrides affect both configured workspace tools and catalog `workspace-plugin` tools.
- A rerun alone is unsafe until the runtime no longer exposes unusable tools and governed implementation steps receive effective software-development tool access.

## Closure Requirement

Add regression proof that a trusted governed process run can override a read-only persisted agent profile to software-development workspace tools, including scaffold/build/test/run tools, and that `workspace-plugin` only exposes tools allowed by the effective runtime profile.
