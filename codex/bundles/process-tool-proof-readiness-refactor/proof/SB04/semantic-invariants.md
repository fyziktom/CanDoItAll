# SB04 Semantic Invariants

- Invariant ID: `SB04-templates-declare-typed-proof`
- Source raw note: `bundle://requirements/01-normalized-requirements.md`
- Expected behavior: Software-delivery QA and screenshot/writeback process steps declare browser and image proof as typed `RequiredReceipts`.
- Disallowed shallow implementation: Do not leave proof requirements only in process prose or common MAF prompt customization.
- Failing-first test: `Software_delivery_qa_steps_declare_conditional_browser_and_image_receipts`
- Passing test: `Dotnet_ui_screenshot_writeback_keeps_development_instructions_in_process_scope`
- Changed source files: `repo://Templates/Processes/processes/software-delivery/definition.json`, `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- Production assertions: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessCapabilityScopeContractTests.cs` asserts migrated template receipt tools and conditional activation.
- Red-team negative case: Template tests fail if QA steps omit `browser_take_screenshot` or image-analysis receipts.
- Downstream dependency check: `bundle://proof/SB04/transcripts/web-build.log` proves the web app rebuilds with the migrated templates available for the 5032 instance.

