# Target Solution

## End State

- Agent inventory has one authoritative technical source in the Agent Framework, while CRM-HR acts as a search and profile surface layered over that source.
- CRM-HR retains ownership of CRM-HR-specific party and profile metadata, but discovery does not depend on a CRM-HR party already existing for every technical agent.
- The Processes workspace and database profiles dialog are usable again in the browser without introducing ad hoc CSS or UI-only hacks.
- Showcase provisioning is routed through the existing process-template pack and process runtime services, with any missing orchestration filled in through extension of that path instead of by copying the hardcoded simulation seeder model.
- The end-to-end showcase produces concrete project, process, agent, and runtime evidence in the requested database and records execution findings in this bundle.

## Boundaries

- UI modules remain thin orchestration layers. Inventory composition belongs in services and bridges, not in page components.
- Template-driven process creation stays in `CanDoItAll.Modules.Processes` and existing template assets. The scenario seeder may call into those services, but should not become the new source of process truth.
- Agent tool and capability wiring must reuse existing agent metadata and configuration surfaces so that CRM-HR and process runtime can consume the same definitions.
- Bundle proof must combine code-level validation, targeted tests, and browser or runtime evidence. No single layer is sufficient for final closure.
