# C# Architecture Anti-Pattern Red Flags

## Fake separation

- Adding another partial class to the same large type.
- Moving code into nested classes inside the same large type.
- Creating a `Helpers`, `Manager`, `Utils`, or `Common` class that owns unrelated responsibilities.
- Creating an interface implemented only by the old monolith without moving behavior.
- Creating a facade that becomes the new monolith.

## Dependency mistakes

- Injecting `IServiceProvider` into domain, core, builders, strategies, or providers without a narrow factory reason.
- Calling `BuildServiceProvider` while registering services.
- Directly instantiating concrete dependencies inside services that should be testable.
- Letting singletons capture scoped services.
- Referencing implementation projects from contracts/core.

## Testability mistakes

- Unit tests need a database, filesystem, network, external API, or full app host for pure behavior.
- Tests assert only object creation, row count, non-empty output, or diagnostic strings.
- Tests do not include a negative or variation case for a critical extraction.
- Tests still instantiate the old large runtime when they claim the behavior was extracted.

## Bundle mistakes

- Architecture is described only in prose without a dependency map.
- Subbundles are split by files instead of responsibilities.
- No checkpoint exists after foundation extraction.
- New capabilities are added before extension seams are proven.
- Residual risks hide work that should be a follow-up subbundle.
