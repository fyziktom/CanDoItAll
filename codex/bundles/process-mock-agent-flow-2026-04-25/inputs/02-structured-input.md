# Structured Input

## Desired Capability

- Enable deterministic process automation runs with mock agents.
- Keep the feature settings-gated and disabled by default.
- Use multiple role-specific mock agents rather than one generic scenario operator.
- Create and hand off artifacts through the normal AgentFramework workspace artifact pipeline.
- Exercise a calculator delivery process with an intentional QA rejection and repair loop.

## Explicit Non-Goals

- Do not connect to real LLM agents for this test mode.
- Do not rewrite the process dispatcher.
- Do not add broad UI work in this bundle unless needed to expose the setting.
- Do not rely on stringly typed hidden fallbacks that silently mask errors.

## Validation Target

- Unit or integration proof that the setting controls mock catalog availability.
- Runtime proof that deterministic mock agents write expected artifacts.
- Process-flow proof that QA can return work for repair and then approve after repair.
