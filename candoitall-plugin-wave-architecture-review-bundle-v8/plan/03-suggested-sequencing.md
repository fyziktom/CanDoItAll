## Suggested sequencing

1. **Hierarchy + node-core/binding split first**
   - these change canonical ownership and should happen before expanding plugin surfaces.

2. **Registry/capability centralization second**
   - otherwise CRM/HR and plugin rules will be built on split semantics again.

3. **Plugin-first editor and resolution flow third**
   - now the connector platform can grow without dragging legacy enums further.

4. **Durable connector-operation boundary fourth**
   - make this the last prerequisite before write-side plugins actually land.

5. **Hotspot decomposition after the hard gates**
   - do it once the ownership model is stable, not before.
