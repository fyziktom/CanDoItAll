# OpenAI Cost Reconciliation Prompt

Use this prompt when investigating provider billing mismatch.

1. Collect all provider usage observations for the target process run or time window.
2. Group by provider name, model, provider response id, source phase, execution run id, process run id, and workflow id.
3. Export normalized fields: input tokens, cached input tokens, output tokens, reasoning tokens, provider total tokens, usage status, raw usage JSON presence, calculated cost, provider-native cost when available.
4. Compare with OpenAI billing/export data or manually downloaded usage CSV/API output.
5. Report:
   - exact matched response IDs,
   - internal-only observations,
   - provider-only observations,
   - known deltas,
   - unknown usage states,
   - final confidence level.

Do not mark reconciliation solved when only internal estimates exist.
