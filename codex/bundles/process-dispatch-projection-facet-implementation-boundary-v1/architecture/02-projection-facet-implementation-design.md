# Projection Facet Implementation Design

## Split strategy

1. Keep the facet interfaces internal and module-local.
2. Split implementation by facet group.
3. Use top-level internal classes where possible.
4. Use delegate-based adapters for instance-only side effects such as `EnsureStepDispatchClaimHeldAsync`.
5. Keep file IO in a dedicated explicit side-effect facet.
6. Keep candidate mutation centralized.
7. Avoid a single class implementing all facets.

## Dependency principle

A coordinator must depend only on the facets it actually uses.

Examples:

- Execution artifact coordinator should need claim guard, file IO, path resolver, classifier, expectation matcher, candidate state.
- Process mock coordinator should need process mock rules, path resolver, file IO, classifier, candidate state.
- Browser coordinator should need browser rules, session observation, path resolver, file IO, classifier, expectation matcher, candidate state.
- Completed decision coordinator should need decision rules, lineage factory, candidate state, record-only coordinator.

## Do not over-correct

Do not move these abstractions into a separate project yet. Keep them internal to `CanDoItAll.Modules.Processes`.
