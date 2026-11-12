# Product-owner Walkthrough Validation — 2026-08-23

## Scope and reliability

This compares the product-owner’s 37-minute Czech walkthrough with the reconstructed
product/domain/UX documentation. Its source is the adjacent
[timestamped transcript](product-owner-walkthrough-2026-08-23.transcript.md), generated
locally from `2026-08-23 17-43-44.mkv` using `faster-whisper` `base`.

The recording is strong evidence of intended end-to-end behaviour, but the automatic
transcript is noisy in Czech. Findings below use only portions whose meaning is clear
from the transcription and surrounding demonstrated context. Timestamp ranges point
back to the recording; they are not presented as verbatim quotations.

## Executive finding

Yes: the walkthrough materially strengthens the reconstruction. It confirms that the
product’s primary story is not a collection of unrelated project, agent, and process
screens. It is a governed path from incoming customer work through AI-assisted analysis
and planning into scheduled, resourced execution—with explicit authority, escalation,
artifacts, and recovery.

It does not contradict the existing documents. It resolves three formerly weaker
connections: how a project commonly starts, how agents help shape a project plan, and
why process escalation belongs in the core experience.

## Claim-by-claim comparison

| Walkthrough finding | Recording evidence | Status against current design docs | Documentation outcome |
|---|---|---|---|
| A customer email/request can initiate a project-oriented flow; the demo categorises and summarises the email before work proceeds. | 00:01–01:51 | The docs had Project creation but no concrete intake-to-project story. | Added Scenario 1. Treat connector/category mechanics as implementation-specific until public contracts confirm them. |
| A Simple Chat is useful for bounded research/architecture work before a fuller agent-led plan. | 01:53–06:28 | Confirms the Simple Chat/Agent distinction and adds a practical handoff example. | No semantic change; Scenario 1 now makes the journey explicit. |
| Agents can propose a plan of tasks with timing/dependencies, which can be shown in Gantt; Gantt remains editable and dependency-aware. | 06:28–12:02 | Confirms project structure and Gantt as complementary views of delivery truth. | Added `Delivery plan` vocabulary and reinforced the projection principle. |
| Creating project-structure work is a specific agent authority, not an automatic consequence of being able to chat. Agents may also be allowed to create subprojects. | 06:57–10:17 | Strengthens the existing governance claim; the exact permission taxonomy is not yet established. | Added `Tool grant / authority` vocabulary and a product invariant. Keep exact grants/API names open. |
| A plan can account for assigned people/resources and their cost; an AI-generated schedule may respect ordinary working time such as weekends. | 07:28–07:48; 12:47–13:54 | Consistent with task, assignment, capacity and costing evidence. | Confirmed as a planning expectation, not a promise that every scheduling mode optimises calendars. |
| Processes compose reusable role-driven subprocesses; agents in roles need explicit project-structure/storage access. Missing access produces escalation. | 21:22–23:44 | Confirms the current Process/Agent/approval model and its practical reason. | Refined Scenario 4 exception narrative. |
| An escalation for a missing artifact can be investigated in context, repaired with the relevant agent(s), then continued; run history, chats and artifacts remain discoverable. | 25:20–30:49 | Strongly corroborates governance/traceability and recovery principles. | Refined Scenario 4; retain this as a key redesign journey. |
| Providers can be local or hosted and an agent selects a model/thinking effort; provider configurations may support image/vision. | 31:57–32:49 | Consistent with Provider Profile/configuration vocabulary. | No change: individual provider features remain configuration evidence, not global requirements. |
| Human approval gates and per-agent choices govern tools, storage read/write, MCP, skills and related capabilities; secrets remain protected configuration. | 32:52–34:18 | Corroborates existing safety and capability distinctions. | No semantic change; strengthens priority of clear authority UX. |

## Design consequences for a future UI

1. Make the **intake → analysis → plan → execution** path discoverable without making a
   particular email connector mandatory.
2. Treat an agent’s proposed plan, its authority to apply changes, and a person’s
   approval as separate states. Do not make a polished agent response look like an
   already-committed project plan.
3. Show the same delivery truth as structure, schedule, assignments/cost and artifacts
   without turning those views into separate sources of truth.
4. Design escalation as a productive recovery workspace: identify the blocked step,
   missing artifact/access, responsible role/agent, relevant discussion, action to
   repair, and safe continuation.
5. Present provider/tool/storage configuration as prerequisites and scopes of authority,
   not as incidental advanced settings.

## Items deliberately not promoted to requirements

- The precise intake implementation (Microsoft 365/Power Automate-like labels appear in
  the recording) is not a documented generic product contract.
- The transcription does not reliably establish the complete agent permission matrix,
  exact role-level names, or whether every agent may create subprojects.
- Mentioned image, vision, voice and individual provider capabilities are configuration
  examples. They should not drive the product’s information architecture without
  separate contract and stakeholder review.
- Several moments in the recording are explicitly demonstrations of bugs or incomplete
  behaviour; they are not product requirements.

## Follow-up validation worth doing

- Have the product owner review the five design consequences above, especially the
  intake boundary and approval/authority language.
- If the video is used as a primary stakeholder record, manually correct its Czech
  transcript around 06:28–13:54 and 21:22–30:49 before quoting or translating it.
- Add one end-to-end automated scenario covering an intake-derived project, authorised
  plan application, blocked process artifact, escalation, repair, and continuation.
