# Impacted-test protocol

## 1. Freeze the subbundle diff

Record:

- subbundle base SHA;
- candidate head SHA;
- `git diff --name-status`;
- `git diff --unified=0`;
- only production/test files intentionally changed by this subbundle.

Derive one-based inclusive changed line ranges. Do not seed the query with every file read during investigation.

## 2. Prepare workspaces

Supply every runnable workspace relevant to the affected area. Typical candidates:

- `tests/Solutions/CanDoItAll.Tests.Components.slnx`
- `tests/Solutions/CanDoItAll.Tests.Unit.slnx`
- `tests/Solutions/CanDoItAll.Tests.Integration.slnx`
- `tests/Solutions/CanDoItAll.Tests.Playwright.slnx`
- `tests/Solutions/CanDoItAll.Tests.Stable.slnx`

Do not automatically supply or run all of them. Include each when source ownership, references, or known consumers make it relevant.

## 3. Conservative query

Call `code_analytics_impacted_tests_get` with:

- repository root;
- relevant test workspaces;
- actual `changes`;
- `contextOnlyPaths`;
- `behaviorIntent=Unknown`;
- sufficient selector/reason limits to avoid unsafe truncation.

Verify:

- every workspace is healthy;
- source-test discovery is nonzero;
- every changed range resolves to the intended symbol and shape;
- ignored context-only paths are reported as expected;
- confidence and fallback scope are understood.

## 4. Optional behavior-preserving query

Only when the conservative result and source review prove a body-only behavior-preserving implementation, repeat with `BehaviorPreservingImplementation`.

Do not use this value merely because a signature stayed the same. Razor markup, parameters, CSS, accessibility, callbacks, and composition can change behavior without changing a C# signature.

## 5. Execute proof

Run every required selector.

- exact method selectors match one intended test;
- class/namespace selectors use the returned fully qualified prefix;
- project/workspace selectors run the returned path unfiltered;
- `AllSuppliedSuites` runs every supplied workspace;
- every command must discover a nonzero and expected number of tests;
- zero or unexpected discovery invalidates proof.

Conditional selectors may remain deferred only while all containment assumptions remain true.

## 6. Promotion

Promote conditional selectors when:

- the returned trigger occurs;
- a required test fails;
- the change expands;
- DI, reflection, dynamic dispatch, generated code, serialization, or public contract uncertainty appears;
- workspace health degrades;
- rendered behavior differs from baseline;
- a new consumer is discovered;
- architecture proof changes the project boundary.

## 7. Record

Store request, response, selectors, discovery counts, commands, results, deferred conditionals, containment rationale, and promotion decisions in the subbundle proof manifest.
