# Source Index

Use these sources as conceptual anchors when making architecture decisions.

## Microsoft .NET dependency injection guidelines

Key takeaways:

- Services should be small, well-factored, and easily tested.
- Direct instantiation of dependent classes inside services creates coupling.
- Many injected dependencies can indicate too many responsibilities.
- Avoid service locator patterns and avoid calling `BuildServiceProvider` during registration.
- Use factories for limited-lifetime disposable instances and controlled creation.

## Microsoft .NET unit testing best practices

Key takeaways:

- Good unit tests are fast, isolated, repeatable, self-checking, and timely.
- Unit tests help expose tight coupling.
- Unit tests should avoid infrastructure dependencies that belong in integration tests.
- Arrange, Act, Assert helps keep tests readable.
- Test names should describe method, scenario, and expected behavior.

## Microsoft clean architecture guidance

Key takeaways:

- Application Core should not depend on Infrastructure.
- UI or composition root wires implementations through interfaces.
- Clear project responsibilities make unit testing easier.
- Infrastructure implements interfaces defined by inner layers.

## Refactoring.Guru C# design patterns

Use the catalog as vocabulary for selecting patterns when the problem forces justify them:

- Builder
- Factory Method
- Abstract Factory
- Adapter
- Bridge
- Facade
- Decorator
- Command
- Chain of Responsibility
- Observer
- State
- Strategy
- Composite
