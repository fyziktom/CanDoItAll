# Normalized Requirements

## Functional Requirements

1. CRM-HR agent discovery must surface the same existing technical agents shown in the dedicated Agents module without introducing a second source of truth.
2. CRM-HR agent editing must continue to support CRM-HR-specific profile data and bindings for those same agents.
3. The `/processes` workspace must allow vertical scrolling and usable interaction on the affected desktop viewport, not only on mobile overrides.
4. The database profiles dialog must expose copy affordances for every user-visible database or workspace path shown in the modal.
5. The showcase must target `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`.
6. Showcase provisioning must create or reuse project structure, process definitions, roles, and agent resources needed to deliver a Blazor SSR calculator application.
7. Showcase provisioning must use the existing template system and projection mechanisms for processes and related role or step structure. Hardcoded showcase-specific process definitions are not acceptable.
8. UI-oriented showcase agents must be provisioned with the capabilities needed to use Playwright and to process screenshots.
9. The live showcase run must exercise the expected delivery path from project structure into process execution, resource sourcing, artifact handoff, QA validation, and progress updates until the application is done.
10. Every missing implementation detail, defect, or operational gap discovered during the live showcase must be recorded in the bundle artifacts and fixed when required for the final pass.

## Non-Functional Requirements

- Prefer the smallest maintainable change that keeps the technical source of truth singular.
- Reuse existing tests, template infrastructure, and agent metadata patterns whenever possible.
- Keep evidence strong enough to reject a false positive pass. The bundle does not close on “mostly worked.”
