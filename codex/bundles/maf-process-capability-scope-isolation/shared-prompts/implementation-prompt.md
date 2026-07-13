# Implementation Prompt

Use this prompt when executing any subbundle.

```text
You are implementing part of the maf-process-capability-scope-isolation bundle.

Read the root README, plan/01-phase-plan.md, architecture files, requirements, traceability, and the current subbundle README before changing code.

Keep common MAF domain-neutral. Do not add software-delivery, UI-design, browser-proof, or Blazor prompt behavior to common workspace tools. Process step scope must be strongly typed and must suppress capabilities before they enter agent context. Do not rely on prompt instructions alone for tool, skill, or MCP limitation.

Use the existing capability access evaluator wherever possible. Remember that Allow is not restrictive in the current evaluator; suppression requires Deny or an explicit allow-only compiler/evaluator change. Required capabilities must be passed as real requirements and must fail predictably when absent or denied.

Respect C# boundaries: process core/template/runtime contracts remain MAF-independent; AgentFramework integration translates process scope into MAF metadata and policy. Add tests proportional to the phase and capture proof under proof/SBxx/.
```
