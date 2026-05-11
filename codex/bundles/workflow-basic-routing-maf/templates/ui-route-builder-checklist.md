# UI Route Builder Checklist

## Edge Inspector

- Route mode selector: Direct, If predicate, Switch case, Switch default, Fan-out selector.
- Label input with fallback summary.
- JSON path field with examples such as `$.status` and `$.decision.kind`.
- Operator selector filtered by route mode where useful.
- Expected value kind selector.
- Expected value input with JSON validation for non-string values.
- Case-sensitivity toggle for string operators.
- Fan-out target index/order field for fan-out routes.
- Legacy `ConditionExpression` display only as compatibility data.

## Visual Feedback

- Show route mode badge in edge list.
- Show route summary on connector or edge row.
- Highlight invalid route fields.
- Show switch default branch clearly.
- Keep controls readable on maximized and narrower viewports.

## Browser Proof

- Create IF route.
- Create switch default.
- Create fan-out route.
- Validate incomplete route.
- Save and reload.
- Run preview and verify the expected branch.
