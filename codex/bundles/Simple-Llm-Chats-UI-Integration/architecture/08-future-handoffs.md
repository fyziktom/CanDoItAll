# Future Handoffs

## Project Context Bundle

A later bundle should add typed `ConversationContextItem` / source references and a Workbench adapter for:

- whole Project Structure preview/snapshot;
- selected node;
- selected node with subtree;
- token estimate, omissions, fingerprint, and one-turn versus pinned lifecycle.

The current `ConversationComposer.ContextActions` slot is the intended UI seam. Do not add a disabled button or prompt-text prefix in this bundle.

## Enterprise Chatbot Deployment Bundle

A later `LlmChatDeployment` aggregate should own:

- channel/widget identity;
- external participant/SSO mapping;
- moderation, rate limiting, retention, legal hold, residency;
- human handoff and deployment-specific system policy.

Reusable `LlmChatDefinition` remains the versioned behavior/model configuration and must not absorb deployment fields now.
