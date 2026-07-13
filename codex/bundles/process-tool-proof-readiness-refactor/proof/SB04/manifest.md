# SB04 Proof Manifest

## Changed File Hashes

- `f73eb7e1ddee76824e83871270649f0bfa9069d9840b2ebb1d6514367fca6ea4` `repo://Templates/Processes/processes/software-delivery/definition.json`
- `4b4bd42c77a82f5bdc5d4dea92aa311e32883779dd5ed2ce11d6ce742537a136` `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `569c81c66f8a508f3fbc02706d9be72ba3e151971de9966d53156954c50840a7` `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessCapabilityScopeContractTests.cs`

## Proof Artifacts

- Passing transcript: `bundle://proof/SB04/transcripts/proof-transcript.log`
- Raw focused test log: `bundle://proof/SB04/transcripts/focused-tests.log`
- Web build transcript: `bundle://proof/SB04/transcripts/web-build.log`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/proof-transcript.log`
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Failing-first: N/A - process template migration is validated with template loader assertions and will be exercised by the restarted local process run.

## Test Names

- Test name: `Software_delivery_qa_steps_declare_conditional_browser_and_image_receipts`
- Test name: `Dotnet_ui_screenshot_writeback_keeps_development_instructions_in_process_scope`

