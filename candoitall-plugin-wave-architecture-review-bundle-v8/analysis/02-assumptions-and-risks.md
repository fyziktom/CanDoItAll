## Assumptions and risks

### Assumptions
- The uploaded repo zip is the latest post-bundle7 refactor state.
- The goal is not to demote node into a throwaway view; node must remain the central carrier for mindmap semantics.
- XY placement and markers are part of canonical project meaning.

### Analysis risks
- Runtime evidence is incomplete because `dotnet` was unavailable here.
- Some repo-internal bundle assets and prior review artifacts are included inside the archive. They were treated as historical context, not as proof that the current code is correct.
- The next plugin wave will likely include both read-only and write-side integrations. The architectural guidance below assumes write-side integrations are coming, because email/LinkedIn/custom APIs usually imply side effects, sync, or durable background execution.
