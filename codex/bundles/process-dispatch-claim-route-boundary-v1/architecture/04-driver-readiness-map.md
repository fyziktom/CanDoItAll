# Driver Readiness Map

This is documentation-only in this bundle.

Future process helper drivers will benefit from route/evidence intents:

| Dispatch intent | Future driver relationship |
| --- | --- |
| `SoftwareBuildValidation` | DotNet/Rust/Node helper drivers may produce build evidence. |
| `SoftwareTestValidation` | DotNet/Rust/Node helper drivers may produce test evidence. |
| `BrowserRuntimeValidation` | Browser/playwright helper drivers may produce runtime visual evidence. |
| `DocumentDeliverableValidation` | Office/PDF/document helpers may produce deliverable evidence. |
| `SpreadsheetValidation` | Excel/CSV helpers may produce spreadsheet evidence. |
| `BusinessAnalysisReview` | Business-analysis helpers may produce analysis summaries and assumption/risk evidence. |
| `HumanApprovalOrReview` | Human approval tools may satisfy review/decision evidence. |

Do not implement these drivers now. The goal is to keep dispatch route facts named so a later driver registry can map route/evidence intent to available helper packs.
