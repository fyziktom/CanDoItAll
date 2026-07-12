# Large Class Refactoring Checklist

## Before editing

- [ ] Identify all partial files for the type.
- [ ] List responsibilities by method and dependency.
- [ ] Pick one cohesive slice.
- [ ] Identify existing tests.
- [ ] Add characterization tests if needed.
- [ ] Decide whether extraction target is method, class, project, factory, builder, strategy, adapter, provider, or facade.

## During extraction

- [ ] Extract contracts first if multiple projects are involved.
- [ ] Move implementation to a top-level type.
- [ ] Wire through abstraction.
- [ ] Keep old class as thin delegation only.
- [ ] Delete old implementation code.
- [ ] Avoid service location.
- [ ] Avoid cyclic references.

## After extraction

- [ ] Add isolated unit tests.
- [ ] Add negative test for shallow implementation.
- [ ] Add composition smoke if registration changed.
- [ ] Run build and tests.
- [ ] Record source assertion that old class no longer owns the behavior.
- [ ] Run architecture review gate.
