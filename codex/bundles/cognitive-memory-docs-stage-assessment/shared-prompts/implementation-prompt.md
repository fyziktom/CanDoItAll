# Implementation Prompt

Implement this bundle outcome-first.

Owned inputs:

- Preserve `inputs/00-original-request.md`.
- Use `requirements/01-normalized-requirements.md` as the acceptance source.
- Keep source claims grounded in inspected files listed in `inputs/01-source-artifacts.md`.

Hard constraints:

- Keep changes documentation-only unless a source blocker makes that impossible.
- Do not call Cognitive Memory beta unless the source proves beta readiness.
- Do not treat Qdrant/RAG as canonical memory.
- Do not hide projection rebuild, scheduling, API, provider, or maintainability gaps.

Required proof:

- Dedicated `docs/cognitive-memory` section with subfolders.
- Mermaid architecture-beta, flowchart, class, and sequence diagrams.
- Roadmap with already-done work, next steps, and beta gates.
- Existing docs entry points updated.
- Bundle validator and `git diff --check` pass.

Stop conditions:

- Stop and reopen subbundle 01 if source audit contradicts the stage assessment.
- Stop and reopen subbundle 02 if diagrams describe future design instead of current implementation.
- Stop and reopen subbundle 03 if closure validation fails.
