# Original Request

The user asked to use `candoitall-bundle-workflow` to solve the follow-up cognitive memory work. The original completed bundle is:

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2`

The requested goal is proper refactoring and completion of missing or partially implemented parts of cognitive memory. The user stated that a lot was already implemented or partially implemented, and asked to analyze the original bundle, original plans and features, then analyze the actual implementation to find gaps, missing parts, and parts that must be improved or refactored.

The user specifically asked to analyze possible isolation of shared helpers, models, enums, and splitting too-large files into smaller logical parts for maintainability. The result must be a follow-up bundle, not an ad hoc patch.

Validation requirements from the request:

- Use `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U` read-only for realistic data.
- Prepare LB4U information as bundle inputs, including translated staged summaries.
- Create a new project and load all information about the project during execution.
- Load some LB4U files as asset nodes, such as presentations, PDFs, spreadsheets, and the business plan `LB4U-BP.docx`.
- Feed information in stages instead of one bulk import, because users naturally build project knowledge over time.
- Reverse-engineer likely stages from the final LB4U artifacts.
- Use the data to validate that cognitive memory stores logical, useful chunks and eventually creates aggregated knowledge automatically during dreaming/consolidation cycles.
- Do not manually seed generic business-plan, marketing, expense, or salary knowledge as canonical truth; memory must derive it through normal ingestion/consolidation.
- Probe the system like a human user by asking questions such as what it remembers about business plans, marketing activities, planned expenses, salaries, and LB4U.
- If useful recommendations appear, approve them through the review flow; if knowledge is missing, ask the system to study more deeply and observe whether memories improve.
- Run multiple testing cycles.
- Perform the main testing with OpenAI model `gpt-5-mini`.
- After OpenAI validation works, also test local Ollama model `gptoss20b64k`.
- Ensure the local Ollama model has an adequate output token amount and is not silently truncated by defaults.
- Use `candoitall-api-cognitive-memory` when working with the memory API.
- If the implementation is improved, update the API surface, skill, and documentation so they remain current.
- Add an `.xlsx` workbook with detailed steps, checklists, and references.
- Preserve enough durable context in the bundle to survive multiple compactions and resumptions.

Primary assumption for this prepared bundle:

- This round prepares the follow-up execution bundle and control workbook. Code changes and live cognitive-memory API testing are delegated to the subbundles because they require staged implementation and repeated validation cycles.
