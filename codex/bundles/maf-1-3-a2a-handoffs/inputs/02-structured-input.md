# Structured Input

## Core Objective

- Upgrade and extend the CanDoItAll agent runtime so agents can cooperate through MAF 1.3 A2A/handoff features and process steps reliably produce downstream artifacts.

## Hard Constraints

- Preserve CanDoItAll layering: Models/Core/Maf/Hosting/Modules.Processes/UI must not collapse into one runtime blob.
- Do not hide A2A or handoff failures behind silent fallback mechanisms.
- Do not grant broad file write/build/run/browser tools to every agent by default.
- Do not weaken process artifact validation to get flows moving.
- No XML documentation comments unless specifically requested.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `C:\repositories\agent-framework`
- `snap-20260502224603-ca161729`

## Input Coverage Signals

- `NOTE-01`: Upgrade Microsoft Agent Framework package usage to 1.3/latest for the .NET agent runtime.
- `NOTE-02`: Make `gpt-5.4-mini` the default model for OpenAI-backed agents, provider adapters, managed seeds, and tests.
- `NOTE-03`: Add A2A support so agents can discover/call remote A2A agents and selected CanDoItAll agents can be exposed through A2A when hosted.
- `NOTE-04`: Add handoff workflow support so process and agent orchestration can transfer work between agents with durable state.
- `NOTE-05`: Software-delivery flows must require QA-consumable artifacts, validation evidence, and concrete handoff summaries.
- `NOTE-06`: Agents assigned to software development or business analysis must receive the tools they need without over-broad default permissions.
- `NOTE-07`: Check context/session/compaction limits and repair policies that cause governed process agents to lose necessary context.
- `NOTE-08`: Include architecture review checkpoints every few subbundles and add refactor subbundles if direction is wrong.
- `NOTE-09`: Use local MAF clone guidance.

## Dependency And Sequencing Signals

- MAF package/API upgrade must happen before A2A/handoff implementation.
- Default model work can proceed after package breakage is known, but before validation closure.
- A2A and handoff runtime must be in place before process flow integration.
- Tool profiles and context policy must be reviewed before process integration, otherwise process proof can be misleading.

## Validation Expectations

- Targeted `dotnet build` for AgentFramework Core/Maf/Hosting and affected Modules.
- Unit tests for model serialization, provider defaults, feature matrices, tool profile rules, and handoff/A2A guards.
- Integration tests for Maf runtime and process artifact handoff.
- Browser validation only when visible Blazor UI changes are made.

## UI Validation Strategy

- UI is not a primary goal. If editor panels change, validate the affected route at desktop and a narrower viewport with screenshots and basic interaction assertions.

## Browser Validation Analytics

- Any UI subbundle must log route, viewport, actions, assertions, screenshot path, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- `gpt-5.4-mini` is available to the configured OpenAI provider.
- A2A preview packages are acceptable if isolated behind adapter boundaries.
- Existing process artifact validation is kept and strengthened, not replaced.

## Primary Risks

- MAF 1.3 breaking changes.
- Preview A2A types leaking into stable Core contracts.
- Recursive handoff loops.
- Tool overexposure for developer agents.
- Context/session policy dropping upstream artifact evidence.
