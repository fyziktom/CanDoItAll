# Original Request

User reported that the previous process-run-with-agents work is on the right path but still does not work properly with real agents.

Important raw notes from the request:

- User started a real-agent process using DB `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\bf40a76da44f4d0f858dc55f428483c8\db\candoitall.db`.
- The process reached Step 3: `Implement feature, tests, and migration notes`.
- Executor: `Programming Workspace Analyst`.
- UI showed `Failed`, `5 attempts`, `Failed / Failed`, `Missing required artifacts: Migration and rollout preparation checklist`.
- Console showed repeated `Agent repeated identical tool invocation` failures for `workspace_write_file` on calculator files.
- Console showed missing required tools across attempts, including `workspace_dotnet_build` and `workspace_dotnet_test`.
- User suspects the migration/rollout artifact may be inappropriate for an app with no DB, or at least needs deeper exploration.
- User wants mock agents improved to cover these possible failures.
- User explicitly wants isolated phases rather than testing the whole scenario/process.
- First useful proof should test one agent implementing an application.
- Then use a simpler process, roughly three agents, to test artifact outputs and handoffs.

Requested workflow:

- Use `candoitall-bundle-workflow`.
- Solve this as a bundle.
