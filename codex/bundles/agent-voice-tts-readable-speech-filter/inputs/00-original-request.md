# Original Request

User follow-up on 2026-05-18 after testing agent chat voice:

> It is working well in agent chat. I already tested it.
> you must use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to create follow up bundle that will improve text before sending to TTS.
>
> Here if some real example answer from the agent:
>
> "Proven (what I inspected)
> - Source: I read the open projects with the project structure API (project_structure_projects_list).
> - Total returned: 12 active projects.
> - All have status = "Active" and recent updatedAtUtc timestamps (May 17-18, 2026).
> - Common currentPhase values observed:
>   - "Historical plan normalization and memory validation" - appears for three projects named "AI Tap Intelligent Water Faucet" (IDs: a845e5c9..., bf8ba85a..., 80322360...). Updated at 2026-05-18 and 2026-05-17 times.
>   - "Multi-cycle memory validation" - appears on several projects (e.g., ClinicFlow SaaS Business Plan id 5128a19c..., FieldOps Mobile App id 02b5df43..., KnowledgeOps Dashboard id 62a92231..., etc.).
>   - "Concept and pre-FID plan normalization and memory validation" - appears for three projects named "Curacao Glass Recycling And Foam Glass Plant" (ids: 76770384..., 77559150..., e6e52a7e...).
> - Many projects have empty primaryCustomerName, primaryDeliveryUnitName, and primaryOwnerName (no owner/customer metadata set).
>
> Notable items (evidence with IDs)
> - Duplicate-name groups (each group has multiple project IDs with the same name):
>   - "AI Tap Intelligent Water Faucet": a845e5c9-43b5-4885-b970-7a63474029c3; bf8ba85a-ea94-41af-9094-65878b7e732e; 80322360-840f-477c-921c-e07346b57ce3.
>   - "Curacao Glass Recycling And Foam Glass Plant": 76770384-d515-40ce-9924-78a4a59b4f86; 77559150-9b7a-490d-8276-cb083ea5865f; e6e52a7e-e879-4976-85c7-cb9f6cf07862.
> - Example singletons: ClinicFlow SaaS Business Plan (5128a19c-2c76-4ea6-9458-349616e2c383), FieldOps Mobile App (02b5df43-3627-4c83-9940-794a7c2fe53e), KnowledgeOps Dashboard (62a92231-7ba9-4ac4-9bcb-d78cf72b7b7f), Docker Development Platform Analysis (5eef3db8-a958-4cea-85b9-670735e515cd), Regional Inflation And Small Business Economy Analysis (e342f056-39cc-47fb-8380-a07bfdd43e3f).
>
> Inferences and recommended next steps (smallest defensible remediation)
> - Why this matters:
>   - Duplicate project names/IDs can fragment ownership, artifacts, and automation (seams you earlier asked to align with - identity, assignment, provider, rights).
>   - Missing owner/customer metadata reduces ability to assign process roles or integrate with CRM/HR identity seams later.
> - Small, safe next actions I can take for you (pick one or more):
>   1. Inspect a specific project in detail (project_structure_read) - I can open any project by ID and report nodes, files, and declared implementation/output paths. Recommend starting with one duplicate group (for example, the three "AI Tap..." IDs) to decide which to keep. Tell me which ID to inspect or say "pick newest".
>   2. Produce a short "project-structure-context-brief" that records the canonical project chosen and the resolved implementation directory / output path (required if we need later automation or build links). I can create that file in the project structure when you pick a canonical project.
>   3. Run a small cleanup checklist: add primaryOwnerName where known, and flag duplicate projects for consolidation (I can create WorkItem nodes for that work).
> - Quick decision heuristic if you want an automatic suggestion now: prefer the project with the most recent updatedAtUtc or the one you've asked to keep. I can compare the three "AI Tap..." projects and recommend the canonical ID.
>
> Would you like me to:
> - inspect one of these projects now (tell me an ID or say "pick newest" for a given name), or
> - create a brief that records the canonical project and next cleanup tasks?"
>
> as you can see there is lots of IDs in the answer. it will happen often, but we do not want to convert to speach them too. it is anoying. You must improve our driver, so it can remove those ids if they are there (usually GUIDs, the comple ones will be easy to find, those shortened i am not sure if you can do it safe) and add sentence that "during the speach I skipped to telling exact IDs, but you can find them in my text response". If it is already told in that conversation it should not tell it everytime. So there must be some option to suppress adding of this sentece.
> User can see them, but we will save a lot of time and tokens for TTS.
> analyze it, prepare bundle and then execute it to implement it and validate it.
