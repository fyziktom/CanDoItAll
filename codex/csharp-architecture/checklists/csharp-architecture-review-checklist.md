# C# Architecture Review Checklist

Use this checklist before closing architecture-heavy C# work.

## Responsibility

- [ ] Each changed type has one clear responsibility.
- [ ] No new broad `Manager`, `Helper`, `Utils`, or `Common` class was introduced.
- [ ] Old large classes lost responsibilities or became thinner.
- [ ] Moved behavior is not duplicated in the old class.

## Partial classes

- [ ] No new partial class was added, or it is explicitly allowed by policy.
- [ ] Partial files are not used as the final separation strategy.
- [ ] Temporary partials have removal subbundles.

## Project boundaries

- [ ] Contracts are SDK-free.
- [ ] Core does not reference infrastructure, UI, or implementation projects.
- [ ] Runtime does not directly reference every concrete provider.
- [ ] Composition root owns implementation wiring.
- [ ] No cyclic references were introduced.

## Patterns

- [ ] Every selected pattern has a Pattern Selection Record.
- [ ] Simpler alternatives were considered.
- [ ] The pattern improves testability or extension.
- [ ] The pattern is not hiding a monolith.

## Tests

- [ ] Extracted behavior has direct unit tests.
- [ ] Critical behavior has a negative test.
- [ ] Wiring changes have a composition smoke.
- [ ] Tests do not require full runtime construction for isolated behavior.

## Proof

- [ ] Build passed.
- [ ] Targeted tests passed.
- [ ] Source assertions recorded.
- [ ] Dependency map recorded.
- [ ] Architecture gate result recorded.
