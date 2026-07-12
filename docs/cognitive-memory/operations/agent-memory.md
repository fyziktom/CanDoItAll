# Agent Memory Configuration

Memory access is configured per agent. Registering a transport or creating a provider profile does not grant an agent access to that provider.

## Provider Bindings

An agent can bind zero, one, or many enabled provider profiles. Each binding has:

- a validated alias used by prompts and diagnostics;
- an exact provider instance id;
- an explicit position used for deterministic fan-out and merge order;
- an `Optional` or `Required` failure policy;
- a flag controlling whether automatic mode includes the binding.

The runtime calls only bound providers. It never selects the first registered provider and never substitutes another provider after a failure.

## Invocation Modes

| Mode | Behavior |
| --- | --- |
| `Disabled` | Performs no automatic memory query and exposes no contextual memory. |
| `Automatic` | Queries the bindings marked for automatic context, with bounded concurrency, then merges results in configured order. |
| `ExplicitDirective` | Performs no contextual query unless the current user message starts with one or more `/mem:<alias>` directives. |

Automatic context queries are synchronous because the result must be attached to the same model invocation. An explicitly invoked query tool may use an asynchronous provider when that provider also implements operation status; the status result returns the persisted final provider output.

## Explicit Directives

Use a leading directive to select a bound alias:

```text
/mem:team-memory Summarize the customer history relevant to this request.
```

Multiple leading directives select multiple bindings:

```text
/mem:team-memory /mem:project-memory Compare the account history with the project decisions.
```

Directive parsing is deliberately narrow:

- directives are recognized only at the start of the current user message;
- aliases are case-normalized and must match a configured binding;
- duplicate, unknown, malformed, disabled, or disallowed aliases fail before provider dispatch;
- quoted text, inline code, fenced code, and directives elsewhere in the message are not commands;
- recognized directive tokens are removed from both the provider query and the message sent to the model.

Attachments, non-text message content, and safe metadata remain attached when directive text is removed. Provider context is framed as untrusted reference data so retrieved text cannot become model instructions.

## Multiple Providers And Failures

The fan-out has a fixed concurrency limit. Completion timing does not change merged output order.

- A failed `Optional` binding produces a visible diagnostic while successful provider results remain usable.
- A failed `Required` binding fails the context contribution predictably.
- Provider sections retain their provider identity and citations.
- Disabled mode, explicit mode without a directive, and any rejected directive make zero provider calls.

## Agent Tools

The current agent-facing surface exposes context query and operation status only, and only when the agent is in `Automatic` mode with memory tools enabled. `Disabled` and `ExplicitDirective` expose no memory tools, so a model cannot bypass the selected invocation mode. Source ingestion, feedback, cancellation, and provider-event acknowledgement remain internal protocol contracts until a configured transport has a complete authorized and durable end-to-end implementation. They are not advertised as working agent tools.

## Validation

When changing agent memory behavior, validate all of the following:

1. settings serialize, save, reload, and reject malformed or duplicate bindings;
2. zero, one, and multiple bindings behave correctly in each invocation mode;
3. `/mem:` is removed from the provider query and model-bound message without dropping attachments;
4. unknown or disallowed aliases cause zero dispatches;
5. optional and required failures have distinct observable results;
6. operation ownership prevents another agent, session, workflow, or process context from reading status.
