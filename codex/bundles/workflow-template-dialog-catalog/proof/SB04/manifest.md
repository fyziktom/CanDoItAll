# SB04 Proof Manifest

## Subbundle

- Subbundle: `SB04 Generic Template Debranding And Large-Screen Proof`
- Status: `Completed`
- Owned raw notes: `N009`, `N010`, `N011`, `N012`, `N013`
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed-File Manifest

- SHA-256 changed-file hash transcript: `bundle://proof/SB04/transcripts/sb04-changed-file-hashes.txt`
- Key changed files:
  - `repo://Templates/Workflows/workflows/default-workflows.yaml` SHA-256 `1674E5A72091B09967F2483A481FA35C70E177038918C951FB40214CDEC12D83`
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor` SHA-256 `F0149D44EDD96C62DD1B678BD620DBCE7C4EDB5A383ADBA3551ACA3F1D2E0F9B`
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs` SHA-256 `2D23A1CA95708D5777AA866DA1628A345069DAE44551A41A05EF04FA37A3B9F0`
  - `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.css` SHA-256 `C52211BB15B670B0D8CB93D1D9C69D9919107D9C4D54ED59EED8DAB844EF3EBA`
  - `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs` SHA-256 `C6CD248058A0BE3DEB468D888BE86DC8BCF5B629764052B18BFF5B0B17ACC21D`
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs` SHA-256 `301DC97D353EE9FA15574602A899BE8ACA5961BEE1C30E6D615019D110A5AAB6`
  - `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs` SHA-256 `2C7E62B869A231F8DEC8B0C5DBA776E6382647D5C58A0D43F90B888E62392D5F`

## Command Transcripts

- Debranding source search: `bundle://proof/SB04/transcripts/sb04-debranding-source-search.txt`
- Template unit tests: `bundle://proof/SB04/transcripts/sb04-template-unit-tests.txt`
- Component behavior tests: `bundle://proof/SB04/transcripts/sb04-component-tests.txt`
- Build proof: `bundle://proof/SB04/transcripts/sb04-build.txt`
- Whitespace check: `bundle://proof/SB04/transcripts/sb04-git-diff-check.txt`
- Browser validation: `bundle://proof/SB04/transcripts/sb04-browser-validation.txt`

## Failing-First Proof

- SB04 depends on SB02/SB03 failing-first tests:
  - `bundle://proof/SB02/transcripts/sb02-failing-first-component-tests.txt`
  - `bundle://proof/SB03/transcripts/sb03-failing-first-component-tests.txt`
- SB04 content-specific failing condition was present in the pre-change template names and exact terms; passing proof is the source search plus unit test forbidden-term assertions.

## Passing Proof

- Passing transcript: `bundle://proof/SB04/transcripts/sb04-component-tests.txt`
- Semantic positive proof transcript: `bundle://proof/SB04/transcripts/sb04-template-unit-tests.txt`
- Unit tests: 11 passed.
- Focused component tests: 5 passed.
- Build: passed with 0 errors. The proof-output build emitted three copy-retry warnings from multiple projects copying identical template artifacts into one proof output directory; the normal Web build before restarting the dev instance completed with 0 warnings and 0 errors.

## Browser Proof

- Viewport: 1680x1000 only.
- Catalogue screenshot: `bundle://proof/SB04/browser/workflow-template-catalogue-dialog-large-offer-filter.png`
- Preview screenshot: `bundle://proof/SB04/browser/workflow-template-preview-dialog-large.png`
- Small/medium viewport passes: intentionally skipped by user request.

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/sb04-anti-stub-audit.txt`
- Red-team review: `bundle://proof/SB04/red-team-fake-proof-review.md`
- No production fixture shortcuts were introduced.
- Preview and adoption tests exercise real page/component service paths.
