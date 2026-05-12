# Routing Contract Proposal

Use this as implementation guidance, not as a paste-only mandate. Keep comments in source code in English.

## Minimal Model Additions

```csharp
public enum WorkflowRouteKind
{
    Always,
    Predicate,
    SwitchCase,
    SwitchDefault,
    FanOutSelector
}

public enum WorkflowRouteOperator
{
    Exists,
    DoesNotExist,
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsTruthy,
    IsFalsy
}

public enum WorkflowRouteValueKind
{
    String,
    Number,
    Boolean,
    Null,
    Json
}

public sealed record WorkflowEdgeRouting(
    WorkflowRouteKind Kind,
    string Label,
    string JsonPath,
    WorkflowRouteOperator Operator,
    string ExpectedValueJson,
    WorkflowRouteValueKind ExpectedValueKind,
    bool CaseSensitive,
    int? FanOutTargetIndex,
    string RoutingLanguage);
```

## Defaulting Rules

- `null Routing` or `Always` means a direct edge.
- Empty `ConditionExpression` remains a direct edge.
- Non-empty legacy `ConditionExpression` should not become executable unless parsed by an explicit legacy compatibility parser.
- New built-in routes use `RoutingLanguage = "built-in-json-v1"`.
- Future ARTL routes use `RoutingLanguage = "artl-v1"` and are rejected until an ARTL compiler is registered.
