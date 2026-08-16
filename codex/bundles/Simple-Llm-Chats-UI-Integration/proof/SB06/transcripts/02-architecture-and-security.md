# Architecture and security evidence

- Snapshot `snap-20260816225805-ae488e90`: fresh, uncached, no blocking errors, no cycles.
- Dependency query `code-analytics_de81ed9d57774f51b6062510cbffc719`: UI module dependencies point to LlmChats modules; Web composition points to UI.
- Solution inventory `code-analytics_b5c6b7a0424b4e0b99d47e9cbcb1f7e3`: `Composition -> LlmChats.Ui`, `LlmChats.Ui -> LlmChats`, `Web -> Composition/LlmChats/LlmChats.Ui`.
- Static forbidden-reference scan: no route, `/chats`, HttpClient, Persistence, Web, EF, Agent Core/tools/skills, or service locator in UI source.
- Sensitive-surface review: the system prompt appears only on Manage-authorized mutation/editor records; list/read projections exclude it. Application error messages and request fingerprints are not projected.
- `git diff --check`: pass.
