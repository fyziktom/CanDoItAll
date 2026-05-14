# Normalized Requirements

| Id | Requirement | Priority | Owning subbundle | Acceptance |
| --- | --- | --- | --- | --- |
| R001 | Provide a shared MCP idle shutdown service that requests host shutdown after a configured inactivity timeout. | Must | `01-shared-idle-shutdown` | Unit test proves shutdown fires after no active operation and no activity refresh. |
| R002 | Prevent shutdown while a tool call is active, even if the idle timeout elapses during that call. | Must | `01-shared-idle-shutdown` | Unit test proves the shutdown request waits until active operation count returns to zero. |
| R003 | Components MCP must default idle shutdown to enabled with a short timeout appropriate for documentation-style lookup. | Must | `01-shared-idle-shutdown` | Source/default test or settings review shows Components has a shorter default than SSH Ops. |
| R004 | SSH Ops MCP must default idle shutdown to enabled with a longer timeout appropriate for remote operations. | Must | `01-shared-idle-shutdown` | Source/default test or settings review shows SSH Ops has a longer default than Components. |
| R005 | Both requested MCPs must mark tool activity through their centralized tool execution wrappers. | Must | `01-shared-idle-shutdown` | Targeted tests/build confirm constructors and wrappers wire the activity service. |
