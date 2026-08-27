# Target Solution

Use existing owners: connectors discover upstream facts; administration returns a typed
catalog/pricing refresh result; pure Models policy reconciles prices; UI applies a successful
result explicitly and resets incompatible fields on a kind-change event.
Persistence and shared publication preserve the chosen catalog without injecting built-ins.

Known price tables enrich returned real IDs only. They are not model-discovery services.
Unknown prices remain absent through editor, save, projection and sharing.
Do not introduce interfaces, projects, partial-class files, global migrations, or model aliases.
