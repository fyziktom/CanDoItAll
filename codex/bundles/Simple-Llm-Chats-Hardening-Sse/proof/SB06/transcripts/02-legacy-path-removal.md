# Legacy execution path removal

At proof commit `d97e21c`, the historical guard finds both the public
`ILlmChatConversationEngine.SendAsync` member and private `SendCoreAsync` implementation. The command
exits 1 as the expected negative:

`NEGATIVE CONFIRMED: pre-CP1 head exposes a parallel inline admission-provider-completion path.`

At implementation commit `a820b867fcf34cd07a93d201a9ffc492c243e647`:

- both inline members are deleted;
- affected test doubles no longer forward or emulate the path;
- provider-runtime tests use explicit admit, invoke, and complete operations;
- the source guard finds exactly one `conversationEngine.InvokeTurnAsync` caller in production:
  `LlmChatOperationExecutor.cs`;
- offset paths, forbidden module references, and production partial expansions remain absent.
