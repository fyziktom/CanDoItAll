# Workspace Settings Core UI decoupling

**Run `prompt.md` with the complete extracted directory.** This assignment first closes
one Resources readiness finding, then implements the first bounded Workspace UI slice.
It does not redo Memory/Resources extraction and does not certify the entire Workspace module.

Review: `components-decoupling` at `20d816dab0f10d4b55efe4b8ff7e0f6af26dc32d` on 2026-09-29.
The live tree already includes Resources. The user's mention of Memory is treated as a
continuation of the latest pushed work, not a request to repeat the older bundle.
Historical task archives are inputs, not additional product changes.

| Phase | Deliverable |
| --- | --- |
| S0 | Reproduce and repair RS-R1: references/catalog refresh must not claim that the exact Registry editor is loaded |
| W1-W5 | Settings shell plus Workspace defaults, Secrets, Files and Provider history; same real renderers in production and a backend-free sandbox |
| Explicitly deferred | Data Sources, Storage and API Access implementation extraction; Providers remains a redirect to Agents |

Read [the prompt](prompt.md), [review](RESOURCES_REVIEW.md),
[scope decision](MODULE_SELECTION.md), [design notes](WORKSPACE_REVIEW_NOTES.md),
[validation matrix](VALIDATION_MATRIX.md), [development loop](DEV_LOOP.md),
[proof limits](PROOF_STATUS.md), [sources](SOURCES.md) and the unchanged
[shared v3 foundation](shared/README.md). [Safety constraints](SENSITIVE_STATE.md)
are part of the assignment, not optional recommendations.

The current repository instructions are authoritative. This package is an executable
module task, unlike its non-executable shared companion. Review commits never pin execution.
All engineering text and code is English; the owner's conversational report may be Czech.
Local signed commits are permitted under existing repository practice. No remote push,
merge, deployment, ordinary application restart or modification of real operator stores is authorized.

Package checks are separate from product proof:

```powershell
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py --root shared
python shared/tools/test_tooling.py
```

The exact completed handoff checks are recorded in [package validation](PACKAGE_VALIDATION.md).
