# SB012 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by prose inventory or non-empty catalog output. The focused integration test starts the app host, calls the production process template APIs, verifies exact required template keys and paths, checks projected envelope step content, validates mermaid output, and checks shell navigation plus `ProcessTemplateLibraryService` visibility.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- deleting or renaming `software-delivery`, `blazor-app-delivery`, or `business-plan-development`;
- changing the catalog relative paths away from `processes/<template-key>`;
- returning duplicate required template keys;
- exposing only the template list API while omitting detail, envelope, mermaid, baseline scenario, or live-run profile routes;
- returning an empty projected envelope;
- removing `/processes` or `/processes/live` from shell navigation;
- removing required templates from the UI template library category.

## Semantic Positive Proof

`bundle://proof/SB012/transcripts/focused-template-catalog-visibility-test.txt` proves the real web composition can start and expose the required catalog through HTTP and UI launch surfaces. `bundle://proof/SB010/transcripts/process-ui-route-template-inventory.txt` proves the source route map that downstream browser E2E work will exercise.

## Anti-Stub Proof

`bundle://proof/SB012/transcripts/anti-stub-audit-template-catalog-test.txt` proves the test uses a started `WebApplication`, production app/API/component mappings, and exact assertions. It also confirms mock/test-server/stub patterns and the obsolete `sourceTemplateKey` assumption are absent.

## Raw-Note Closure

- RN-003 is partially closed for route, catalog, template library, and project-structure launch affordance inventory. UI process-start execution proof remains open for SB013-SB015.
- RN-008 remains open for large-desktop browser proof in SB013-SB015; this gate made no UI production edits.

## Production Behavior Artifact Matrix

No new production signals were introduced in SB010-SB012. Existing route, API, catalog, and template-library behavior is covered by source assertions and the focused integration test.
