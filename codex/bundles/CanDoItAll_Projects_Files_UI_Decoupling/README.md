# Projects Files P2 — UI decoupling with bounded P1 follow-up

Read and execute [prompt.md](prompt.md). This package continues the shared UI-decoupling wave, not a new backend or API-only rewrite.

The reviewed Projects P1 architecture should remain. First repair **P1-R1**, the UI classification/recovery of a known original-project-lifetime refusal, and **P1-R2**, the failed JS import rethrown during modal teardown. Then extract both actual Files surfaces: the single-project dialog and filtered portfolio pane, including real FileBrowser/FileInteraction composition and an independent sandbox. Do not stop after the follow-up.

The source review supports continuation; it does not certify application release readiness. P1's mixed broad run, later focused results and the qualified historical shared-provider selector observation remain distinct. Read [the review](P1_REVIEW.md), [the refusal repair](S0_KNOWN_REFUSAL.md), [the JS lifecycle repair](S0_FORM_JS_LIFETIME.md), [the scope](SCOPE_AND_ARCHITECTURE.md) and [the validation matrix](VALIDATION_MATRIX.md).

New UI validation is **large-desktop only**, primarily 1920×1080. Keep existing Workspace and P1 dependency graphs lean. Paid inference is not authorized.

The `shared/` directory is the complete, unmodified 22-file shared v3 foundation. Its historical audit is not a current module census. [Large-screen policy](LARGE_SCREEN_POLICY.md) narrows this task without changing the sealed shared files.

## Execution handoff

```text
Read and execute CanDoItAll_Projects_Files_UI_Decoupling/prompt.md.
First repair P1-R1 and P1-R2 with failing-first and owning controls.
Then implement both Projects Files surfaces, their independent sandbox and validation.
Preserve Workspace/P1 boundaries and existing file authority.
Use large-desktop-only UI checks. Do not start another module.
```

[Package-only validation](PACKAGE_VALIDATION.md) is not product execution. [The English source register](SOURCES.md) and [Czech review](REVIEW.cs.md) explain the evidence limits.
