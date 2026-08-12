# A07 hosted validation deferral

Date: 2026-08-10

## Operator decision

The operator explicitly chose to keep portability work on the `unix-adoption` branch until both bundles are implemented and locally tested. Default-branch merge, pull-request creation, hosted GitHub Actions execution, required-check policy evidence, and genuine macOS execution must not block implementation progression during this work.

Tracking id: `HOSTED-PORTABILITY-VALIDATION-001`.

## Accepted implementation anchor

- CanDoItAll: `dd78ffa9769ba1d125b8be81a4b303df37c32505` (`unix-adoption`, pushed)
- Components: `8372c1d55f21b349f8e859470b02eeb4421e96ca` (`development`, pushed)
- FileTools: `f31e20d054003348c7557b9634e0838fc5996ae0` (`development`, pushed)
- All three working trees were clean when the anchors were verified.
- The core portable validator passed for 304 indexed files with zero errors and zero warnings.
- Independent A07 local-readiness review is GO in `reviews/21-a07-independent-review.md`.

## Progression decision

B00 may re-anchor to the exact commits above and runtime implementation may proceed using the approved layered validation policy: named regressions first, affected projects/subbundles second, and one local Windows plus Docker/native Linux gate at meaningful phase boundaries. Previously captured unchanged full-suite evidence is reusable under the recorded invalidation policy.

Core Gate C4 remains `DEFERRED`, not `GO`. This exception satisfies only the dependency needed to continue implementation. It does not establish hosted branch protection, merge readiness, general genuine-macOS support, or a release support claim.

## Deferred evidence

- hosted `stable-windows-x64`, `stable-ubuntu-x64`, and `stable-macos-arm64` jobs;
- hosted `portability-static` and `containers` jobs;
- downloaded hosted artifact checksums and redaction scan;
- repository required-check or ruleset evidence;
- genuine macOS build, test, PostgreSQL migration/restart, publish, and headless execution;
- genuine macOS Keychain execution tracked separately as `MACOS-KEYCHAIN-VALIDATION-001`.

## Non-waived rules

- Do not describe macOS or hosted validation as passed.
- Do not claim C4 or final R4 support closure from local evidence alone.
- Do not weaken security, migration, path, process-ownership, or redaction behavior to avoid a host-specific failure.
- Any locally reproducible product, architecture, security, migration, or data-integrity failure remains a normal blocking defect.
- Re-run affected validation whenever production code, tests, build infrastructure, or relevant runtime inputs change.

## Re-entry

When the branch is ready for merge or release, run the active workflow from a default-branch-visible workflow or pull request, collect the exact-run artifacts, complete the deferred macOS and repository-policy evidence, and issue the real C4/R4 decisions without rewriting this historical exception.
