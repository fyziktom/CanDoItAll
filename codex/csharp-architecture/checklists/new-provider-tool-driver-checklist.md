# New Provider, Tool, or Driver Checklist

## Contracts

- [ ] Contract lives in Abstractions or Contracts project.
- [ ] Contract does not expose provider SDK types.
- [ ] DTOs/options/results are repository-owned types.
- [ ] Error model is explicit.

## Implementation

- [ ] Implementation lives in a cohesive project.
- [ ] External SDK usage is isolated in adapter/client classes.
- [ ] Provider/tool/driver metadata is typed.
- [ ] Runtime does not know concrete implementation details.

## Registration

- [ ] Registration uses composition root or module extension method.
- [ ] Catalog/factory supports extension.
- [ ] Future additions do not require editing runtime partial class.

## Tests

- [ ] Contract-independent unit tests exist.
- [ ] Adapter tests use fake responses.
- [ ] Negative tests cover unsupported provider/tool/driver.
- [ ] Composition smoke proves registration.

## Proof

- [ ] No new partial class expansion.
- [ ] Dependency direction valid.
- [ ] Build and targeted tests passed.
