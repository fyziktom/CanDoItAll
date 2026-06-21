# Post-SB32 Provider, Runtime Node, And Agent Chat Follow-Up

## Source

User feedback from 2026-06-20 against the running instance on port `5032`.

## Raw Notes

- Last process run failed in a mostly correct way because OpenAI API credits were exhausted, but the message did not clearly say that. When an API returns quota, billing, or credit exhaustion information, the product must show that clearly.
- In the multiteam development process last run, the `.NET runtime` project-structure node was not filled correctly. The runtime data was placed into notes instead of typed metadata. The `.NET runtime` node should offer a run option in the double-click dialog like the PowerShell runtime node so the app can be launched without copying commands to a terminal.
- Opening agent chat in project structure takes a very long time. Analyze whether the bottleneck is UI or MAF, explain why it is slow, and find a safe speed-up.

## Initial Live Evidence

- `GET http://localhost:5032/_dev/runtime` reported the app ready on the running development database.
- Project `TetrisGame` had a `Run app` node with `objectType = Environment`, `objectSubtype = dotnet-runtime`, and empty environment metadata fields for `projectPath`, `environmentName`, and `localhostUrl`.
- The same node notes included `dotnet run --project src/TetrisGame/TetrisGame.csproj` and the repository root `C:\programovani\dotnet\output`, proving the data existed but was not typed for the runtime launcher.
- `GET /api/agents/{agentId}/chat-workspace` completed quickly, but `GET /api/agents/{agentId}/chat-sessions` and `POST /api/agents/{agentId}/chat-sessions` each took about 16 seconds.
- The Blazor contextual agent window opens only after `GetOrCreateChatSessionAsync` and workspace loading finish, so the user sees a delayed chat surface even before considering render cost.
