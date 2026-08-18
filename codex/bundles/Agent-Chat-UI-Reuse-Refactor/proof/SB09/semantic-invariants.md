# SB09 semantic invariants

- Agent services, persistence, runtime execution, approvals, attachments, voice, and context effects remain outside the neutral UI project.
- The neutral project owns reusable, strongly typed conversation presentation and callback seams only.
- Dependency direction remains `consumer -> Agent adapter -> neutral Conversations components` with no reverse product reference or project cycle.
- Existing Agent catalog, threads, transcript, composer, runtime detail, settings, floating lifecycle, contextual windows, and Process consumers remain active.
- No Simple Chat UI, API, route, filter, context, or SSE behavior is activated.
- The terminal state is `awaiting-user-agent-chat-regression`; a separate Simple Chat UI phase requires explicit user approval.
