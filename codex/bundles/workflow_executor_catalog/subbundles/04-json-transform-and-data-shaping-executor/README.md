# 04-json-transform-and-data-shaping-executor

## Objective

Implement the planned `json.transform` executor for deterministic data shaping.

## Required work

1. Create typed settings model for transform operations.
2. Implement safe operations:
   - Select
   - Set
   - Remove
   - Merge
   - ArrayMap
   - ArrayFilter
   - ArraySort
   - ArrayDistinct
   - ArrayTake
   - AggregateCount
   - TemplateObject
   - ValidateSchema
3. Use safe built-in JSON path syntax; do not add arbitrary C# script execution.
4. Add result shape and configuration schema.
5. Add tests for invalid paths, missing fields, array handling, type handling, and schema validation.
6. Add sample use in workflow template.

## Acceptance checklist

- Common transforms no longer require LLM calls.
- Invalid transforms fail with actionable messages.
- Output is deterministic and bounded.
