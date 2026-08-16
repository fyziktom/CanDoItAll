# Review summary

The repository is ready for a UI preparation phase, but the current chat UI cannot be reused safely by Simple Chats without first separating presentation from agent runtime semantics.

The highest-risk concentration is not one isolated component. It is a chain:

`AgentChatPanel`
→ `ChatWorkspacePanel`
→ Agent models, execution runs, approvals, voice, attachments, prompt gallery, context parsing, and runtime dialogs.

The participant list and settings surfaces have the same pattern at a smaller scale: visually reusable markup is coupled directly to `AgentDefinition`, `ProviderProfile`, agent workload/status, agent permissions, and agent-only tabs.

The safe first phase is therefore:

1. freeze current behavior and consumers;
2. create a neutral presentation boundary;
3. extract visual and interaction responsibilities behind typed projections;
4. keep existing Agent component names as adapters;
5. migrate every existing Agent consumer;
6. prove Agent parity;
7. stop for manual user verification.

This is deliberately not a Simple Chat feature bundle.
