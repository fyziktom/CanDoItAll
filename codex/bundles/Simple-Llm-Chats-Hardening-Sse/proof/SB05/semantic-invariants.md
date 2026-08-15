# SB05 semantic invariants

1. Definition and conversation collections order by `UpdatedAtUtc DESC` and stable id; continuation
   predicates use the same tuple and never an offset.
2. Transcript pages order only by canonical message sequence and use `Take(pageSize + 1)` to derive
   the next cursor without materializing the remainder.
3. Read models query the canonical LLM Chat tables. They do not own writable or duplicated state.
4. Definition tags are fetched once for the bounded definition page. Conversation lists do not load
   transcripts per item.
5. Production turn admission, resume, completion, and compensation use `ILlmConversationTurnStore`.
   The EF adapter does not call the full-document `EfLlmConversationStore.LoadAsync` path.
6. Provider context contains bounded system entries plus the newest bounded non-system entries in
   canonical sequence order. An active pending entry or newly completed assistant entry retains a slot.
7. Cursor kinds are not interchangeable. Invalid payload, timestamp, id, or sequence data fails
   validation rather than silently restarting at the first page.
8. Operation detail evidence is bounded explicitly; overflow fails predictably instead of being
   silently truncated.
