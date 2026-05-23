# Follow-Up Input: Live Blazor Agent Delivery

The user asked to execute the bundle as a senior C# developer and expand it before execution.

Key constraints:

- Processes core/code must remain generic.
- Blazor-specific delivery instructions belong in process definitions, process steps, agents, tools, skills, and project-structure data.
- Agents in CanDoItAll must build the application; Codex may observe, approve escalations, and independently validate after agent completion, but must not help build the demo app.
- Keep cognitive memory disabled for now because it is unstable.
- Use PostgreSQL only.
- Use `gpt-5.4-mini` for all CanDoItAll agents.
- Back up current project-structure data and assets so reruns can start again from basic info.
- Demo output must be under `C:\programovani\dotnet-demo\output`, with per-run subfolders allowed.
- Data loaded into project structure must go through APIs.
- Process runs can contain large data, so the bundle must first improve instructions to record compact data, create summaries, and read only specific raw records when needed.
- During runs, Codex must act as user, confirm escalations, observe the process, and record UX observations, preferably through API records.
- If the final agent-built app is not satisfactory, identify whether the failure is missing skills, agent/tool permissions, bad staffing, weak process design, or runtime automation.
- Add a simplified generic Blazor app delivery process.
- Add a generic Blazor app repair/fix process.
- Add generic Blazor app feature-addition processes for backend-only, frontend-only, and backend+frontend changes.
