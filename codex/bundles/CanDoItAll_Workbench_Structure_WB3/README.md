# Workbench Structure Authoring WB3

Execute [prompt.md](prompt.md). This is an implementation assignment for the next
large UI-decoupling slice, not another audit-only or closure-only run.

Preserve completed WB2 reporting/support and WB1 planning. First reproduce and
repair the existing hierarchy-dialog lifetime issue, then complete the actual
Structure stage, canvas, toolbox, generic node composer/editing, graph gestures,
clipboard and structural hierarchy/conversion dialogs. Keep native owners and
explicitly deferred runtime/file/party integrations working.

The review uses main `bbd9e8de96dc7895abdecc04406766f7aea94c8e` and Components
`24d182c664d0b1f293098643e52caed7384a5d50`. They are provenance, not required
checkout targets. Inspect the supplied working branch before editing.

## Read map

- [WB2 review](WB2_REVIEW.md) and [Czech summary](REVIEW.cs.md).
- [Scope and architecture](SCOPE_AND_ARCHITECTURE.md), [initial repair](S0_HIERARCHY_LIFETIME.md).
- [Canvas and composer](CANVAS_AND_COMPOSER.md), [graph and hierarchy](GRAPH_AND_HIERARCHY.md).
- [Native writes](NATIVE_OWNERS_AND_OUTCOMES.md), [retained integrations](DEFERRED_INTEGRATIONS.md).
- [Validation matrix](VALIDATION_MATRIX.md), [actual journeys](APPLICATION_JOURNEYS.md).
- [Sandbox/development loop](SANDBOX_AND_DEV_LOOP.md), [execution and closure](EXECUTION_AND_CLOSURE.md).
- [Signing](COMMITS_AND_SIGNING.md), [roadmap](ROADMAP.md), [source register](SOURCES.md).
- The byte-identical [shared v3 foundation](shared/README.md).

The archive contains instructions, source references and validation helpers; no
application patch, provider credentials, native test artifacts or font files.
All new product-evidence groups start at `NOT_RUN`. Package tooling does not run
or certify the C# application.
