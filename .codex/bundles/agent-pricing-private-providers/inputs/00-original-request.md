# Original Request

```text
Main goal:
realistic prices for agents work.
identification of private agents.

Architect notes:
- each provider must allow setup of table of prices for each model. If user manually override model in agent settings then they must fill also price. Price for input and cached and output tokens are usually different. Check OpenAI docs. And exact pricelist for openai is here https://developers.openai.com/api/docs/pricing
- For ollama style (local or remote) private models user must be able to setup prices too. We should propose some realistic default value.
- we need those prices to calculate correctly information about cost of process run or workflows runs. We use it in analytics (for example on Live processes page, or in processes runs history).
- each agent that uses some private style of provider it must have badge in UI (where agent is as card) that shows "Private". So user immediatelly see that some agent is on private provider and can use it for sensitive operations.
```
