# Original Request

## Superseding Scope Correction

```text
good point about that financial strategis. he also said he has no markitdown, but lets get back to it after proper refactoring of the MafAgentRuntime. I think it is our main trouble. Because it is as huge class with partical classes instead of proper architecture isolations it is hard to unittest and it keeps us in lots of troubles. I see that in target solution you are mixing it with the domain specific trouble about that financial strategist agent. I see adding that margin calculation, etc. Remove those things.
Lets step back and focus just to refactor and improvements of whole architecture about maf runtime. We must first solve our generic troubles we have. You must repair whole bundle. Focus strictly to improvements of the mafagentruntime and isolation of drivers, strategies, helpers to do real correct split of the responsibilities. You must analyze how it can affect performance and same time how to achieve better testability of those isolated parts, how to help to mock them in case of integration tests and other thigns that will help us to prepare good stable base for future more specific cases and improvements of agents work.
do not do implementation, just improve the bundle.
```

## Deferred Context

- Financial Strategist PDF/MarkItDown/tool reachability remains a useful future validation case, but it is explicitly out of scope for this bundle.
- Quotation extraction, margin calculation, and project-structure writeback must not appear as implementation work in this repaired bundle.
