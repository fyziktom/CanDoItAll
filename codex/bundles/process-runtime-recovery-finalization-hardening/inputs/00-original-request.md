# Original Request

Source: user prompt from 2026-07-07.

```text
Use candoitall-bundle-workflow to solve this.

Main goal:
refactoring and hardening of processes.

Architect notes:
- We have trouble with process runs. Even the simpler ones usually go into some escalation because an agent might miss some tool or access, or the previous step will not deliver some artifacts or maybe they are lost.
- Recovery procedure and retry are not working correctly. Retry should not happen when some artifact or other input is missing. It is useless and it must go back to previous step to finish work and deliver all with help of manager of the process.
- We should have better check if agent did all job. It might happen that agent will lose context, compress context, or lose original info about what to deliver. Agent should split work in process step to do some finalization step to check if all is ready and filled. Then it might handoff with manager that must confirm that all is prepared for the next step. Otherwise it should not move forward.
- One trouble in software development processes is that when code is written it might take all files as artifacts and add them to next agent as input context and it might be too much context so it loses track immediately.
- Maybe agents can have access to the process step instruction and required artifacts as a tool so they can check it during work. Analyze this option or a combination. There might be multiple artifacts and input templates, so the agent might lose them in compressed or lost context. With a tool it can get them during finishing as fresh context for finalizing work.
- Artifacts must be shared across process steps whenever they are connected as input artifacts. We can see connections in the process canvas. There might be bugs when it must take an artifact from previous steps from earlier and not the direct previous step.
- This might need larger refactoring and hardening. Propose larger changes if they make sense and explain why they are necessary and how they help. They must pass C# architecture skills. Parts of processes are not correctly isolated and are split into partial classes. This limits unit testing. Example: ProcessRuntimeEngine.cs and things around processes.
- Processes runtime and dispatcher must remain generic for any enterprise processes, not just software development. Process templates can be domain-specific, for example multi-team dev and .NET subprocesses, but runtime must remain generic.
- Because some process groups require domain-specific code, we have process drivers, but this option is not used much now.
- It might be helpful to map whole logic and architecture of the processes into bundle first. Then define all user stories and exceptions/escalations to see coverage and gaps. Critical edges are where agents use something, need something, and edges between process steps.
- Need high quality senior C# architecture. Map whole process flows and design modular, well testable architecture that covers the main troubles or isolates them so they can be tuned with tests.

Create bundle only now. Do not do implementation yet.
```

## Literal Scope Words To Preserve

- `retry should not happen when some artifact or other input is missing`
- `must go back to previous step`
- `finalization step`
- `manager must confirm`
- `Artifacts must be shared across process step whenever they are connected as input artefact`
- `not direct previous step`
- `runtime and dispatcher must remain generic`
- `whole logic and architecture`
- `all user-stories and exceptions/escalations`
