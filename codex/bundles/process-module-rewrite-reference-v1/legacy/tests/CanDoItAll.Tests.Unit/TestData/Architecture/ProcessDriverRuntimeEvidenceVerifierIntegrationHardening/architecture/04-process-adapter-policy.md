# Process Adapter Policy

Allowed:
- construct request from supplied payload,
- validate supplied evidence references and transcript hashes,
- call transcript verifier alpha synchronously as pure computation,
- return immutable observation envelope.

Denied:
- DI registration,
- generic driver runtime,
- registry/selector,
- file/workspace/storage reads,
- process state mutation,
- persistence,
- manager/scheduler/workflow hook.

Future observation persistence is out of scope and must be separately approved.
