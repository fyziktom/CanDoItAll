# Assumptions And Risks

## Working Assumptions

- Workflow payload `projectId` and nested `project.id` are external JSON protocol fields and may be read by name.
- `ContextWorkspaceScope` should affect context contributors only; workspace tools keep the runtime's configured workspace boundary.
- Empty Cognitive Memory context means no memory was found, not that governance failed.

## Critical Path Risks

- Weakening all Cognitive Memory failures would hide real memory outages. The fix must only skip empty context packs.
- Losing `runContext` would break project-structure lease validation in downstream asset creation.
- A live Office365 run mutates the mailbox by moving the test email category after success.

## Validation Risks

- API-level fakes can miss Graph/OAuth behavior, so live development database proof is required.
- Raw prompt string assertions are brittle for escaped Czech diacritics; tests must parse JSON payload content.

## Reopen Triggers

- A governed run again reports `project-scope-not-provided`.
- A newly created project with no memory blocks workflow LLM execution.
- The summary asset is created outside the starting workflow node.
- The Office365 mark-processed step cannot find `runContext.office365Processing.messageIds[0]`.
