# Original bundle contract carried forward

The first bundle established these durable boundaries:

- Simple Chats are not agents with tools disabled.
- Reusable chat definition and concrete conversation are different canonical concepts.
- Definition revisions are immutable and conversations pin behavior to a revision.
- Provider/model/settings resolve through canonical provider infrastructure.
- PostgreSQL is the production store.
- Database-profile identity and generation must fence use.
- Idempotency must prevent duplicate paid dispatch.
- Usage evidence must survive failed and compensated turns.
- HTTP endpoints live outside `/api/agents`.
- UI and Project Structure context are later work.
- Broad tests must be delayed until final closure.

This hardening bundle does not weaken those decisions. It repairs places where the implementation
created only an apparent transaction/fence/recovery protocol and adds a durable asynchronous streaming
extension consistent with them.
