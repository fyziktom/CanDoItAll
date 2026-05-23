# SB03 Proof Manifest

## Changed Files

The full changed-file hash list is recorded in `bundle://proof/SB03/transcripts/changed-file-hashes.txt`.

Key hashes:

| File | SHA256 |
| --- | --- |
| `repo://Templates/Processes/manifest.json` | `5435E91464C5C11EAAE1B3B901CBA13756754E338AD9EB3E0F5C726971CEF6ED` |
| `repo://Templates/Processes/processes/blazor-app-delivery/definition.json` | `7DBFA8E671A9F4870FC199D88753E179C397BF734EF42B94457C8AC5DBAC1F34` |
| `repo://Templates/Processes/processes/blazor-app-repair-fix/definition.json` | `2D64DA8B41764F8FAA1B289794340F75D866AB3CEE1BD221049891AFBA36279A` |
| `repo://Templates/Processes/processes/blazor-backend-feature/definition.json` | `2C0C6C54F559EAEA1B25275D5FFEE9E189D8DC07AE7D5E55E29DA1AA552EDC97` |
| `repo://Templates/Processes/processes/blazor-frontend-feature/definition.json` | `A9C62B520EDC654C1E4CC8766C41C968E640D3E5B4A41C6EF430F6FD4007A1F9` |
| `repo://Templates/Processes/processes/blazor-fullstack-feature/definition.json` | `3F1FFFF405406263861D4A60FC4960D258F331040CA495163E9D3466D84CBA78` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | `46E52CA83C385D552509169866B57C587535F8958AECEFD4056A2CA9E71B500D` |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle |
| --- | --- | --- | --- |
| Generic Blazor process templates | Template pack files including `repo://Templates/Processes/processes/blazor-app-delivery/definition.json` and sibling Blazor template directories | Process template loader, catalog, projection, and process launch UI/API | Available as reusable process definitions without process-runtime Blazor branching |
| Browser/runtime evidence contract | `validate-blazor-runtime` step in each Blazor process template | Assigned QA/browser-proof agent and downstream release/record steps | Requires restore/build/test, local startup receipt, Playwright navigation, screenshots, browser state, console output, URL, and cleanup |
| Run evidence index and writeback contract | `record-blazor-results` step in each Blazor process template | Delivery manager and project-structure consumers | Requires compact summaries, screenshot references, console status, final verdict, output path, and project-structure evidence writeback |
| Generic repair/revalidation loop | Branch outcomes and repair/revalidate steps in each Blazor process template | Process runtime progression and assigned agents | Failed runtime proof routes to repair, revalidation, and final record artifacts |

## Validation

- Prepared-stage validator passed after bundle repair: `python validate_bundle.py --stage prepared codex\bundles\process-artifact-recovery-and-proof-path-hardening`
- Passing targeted tests: `bundle://proof/SB03/transcripts/template-tests.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Changed-file hashes: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript: N/A process-template contract expansion; no production runtime behavior was added.
- Passing transcript: `bundle://proof/SB03/transcripts/template-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Known Validation Finding

None remaining. The broader template/editor mapping test was repaired to assert projection overrides explicitly instead of treating valid role overrides as mapping failures.
