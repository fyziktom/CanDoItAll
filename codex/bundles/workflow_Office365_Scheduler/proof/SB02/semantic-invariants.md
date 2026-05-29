# Semantic Invariants SB02

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `office365.message-by-address-unprocessed` executor | `bundle://proof/SB02/transcripts/source-assertions-office365-address.txt` | `bundle://proof/SB02/transcripts/integration-office365-address-after-implementation.txt` | plugin descriptor and DI registration tests | `bundle://proof/SB02/transcripts/failing-first-office365-address-before-implementation.txt` |
| Address-filtered one-message payload | `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs` | `repo://tests/CanDoItAll.Tests.Integration/EmailPluginClientTests.cs` | fake Graph test covers server query and bounded fallback | processed/wrong-address negative case in passing transcript |
| No-message success route | `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` | `repo://tests/CanDoItAll.Tests.Integration/EmailPluginClientTests.cs` | no-message payload test covers zero-count success shape | test asserts empty selected message id and no fake message ids |
| Add-only processed category mutation | `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs` | `repo://tests/CanDoItAll.Tests.Integration/EmailPluginClientTests.cs` | fake Graph PATCH assertion preserves unrelated categories | add-only test covers empty source category |

## SB02-INV-ADDRESS-FILTER

- Invariant ID: `SB02-INV-ADDRESS-FILTER`
- Source raw note: R1 and R2.
- Expected behavior: the Office365 executor downloads at most one newest message matching the configured email address and excludes messages with the processed category.
- Disallowed shallow implementation: reusing the category executor, returning an unbounded mailbox page, matching by display text only, or filtering only in test fixtures.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-office365-address-before-implementation.txt`
- Passing test: `Office365_client_downloads_one_unprocessed_message_by_address_with_processed_category_exclusion` and `Office365_client_uses_bounded_fallback_and_ignores_processed_or_wrong_address_messages` in `bundle://proof/SB02/transcripts/integration-office365-address-after-implementation.txt`
- Changed source files: `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs` after SHA-256 `779BA6DE8633313424799E3CF55AEC251579B79A33BEC47584ABA0C51769B161`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` after SHA-256 `764ABDB3A920E6198B1EE2626BDC3CFDA5F25A9C7FFF0FFCC767125BBC08EA80`
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-office365-address.txt`
- Red-team negative case: already-processed and wrong-address messages are ignored by the fallback test; invalid display-name input is rejected before Graph call.
- Downstream dependency check: SB03 templates and SB06 polling can rely on one-message selection, processed-category exclusion, `selectedMessageId`, and `idempotencyKey`.

## SB02-INV-NO-MESSAGE

- Invariant ID: `SB02-INV-NO-MESSAGE`
- Source raw note: R3.
- Expected behavior: no matching Office365 email returns a successful zero-message payload by default with route `no_messages`.
- Disallowed shallow implementation: throwing by default, fabricating a message, or omitting Scheduler context from the output.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-office365-address-before-implementation.txt`
- Passing test: `Office365_address_download_payload_marks_no_message_as_success_route` and `Office365_client_download_by_address_returns_empty_batch_when_no_candidate_matches` in `bundle://proof/SB02/transcripts/integration-office365-address-after-implementation.txt`
- Changed source files: `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` after SHA-256 `764ABDB3A920E6198B1EE2626BDC3CFDA5F25A9C7FFF0FFCC767125BBC08EA80`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs` after SHA-256 `779BA6DE8633313424799E3CF55AEC251579B79A33BEC47584ABA0C51769B161`
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-office365-address.txt`
- Red-team negative case: the no-message test asserts `count` is zero, `messages` is empty, and `selectedMessageId` is empty.
- Downstream dependency check: SB06 and SB07 can separate no-message polling from failure handling.

## SB02-INV-ADD-ONLY-MARK

- Invariant ID: `SB02-INV-ADD-ONLY-MARK`
- Source raw note: R4.
- Expected behavior: mark-processed can add the processed category while preserving existing categories when no source category is configured.
- Disallowed shallow implementation: requiring a source category, clearing unrelated categories, or treating add-only mutation as preview-only behavior.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-office365-address-before-implementation.txt`
- Passing test: `Office365_client_can_mark_processed_by_adding_category_without_source_category` in `bundle://proof/SB02/transcripts/integration-office365-address-after-implementation.txt`
- Changed source files: `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365GraphClient.cs` after SHA-256 `779BA6DE8633313424799E3CF55AEC251579B79A33BEC47584ABA0C51769B161`; `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs` after SHA-256 `E15DC21FBD7C19ABEAA9B10487948000D1F0B781A3F9765ADD028100905C0B6D`
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-office365-address.txt`
- Red-team negative case: the add-only test asserts the existing category remains after PATCH and no source-category removal is reported.
- Downstream dependency check: SB03 templates can omit source category on mark-processed steps while still marking the Office365 message processed.
