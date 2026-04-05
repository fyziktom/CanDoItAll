# Extensible node reference model
The current `ProjectNodeReferenceKind` + `ProjectNodeReferenceSet` model is closed-world. That is incompatible with a long-lived connector ecosystem.

Recommended direction:
- persist open reference rows with namespace/key/target-kind/target-id-string/order/metadata,
- keep typed mappers for first-party features,
- allow plugin-defined relation roles without modifying the core enum or the core fixed property bag.

Do not close this by just adding more enum members.
