# QA Prompt

Review each gate for semantic preservation, not just build success.

Questions:

1. Did projection source order remain unchanged?
2. Were file IO/storage/DB side effects moved only into explicitly named coordinators?
3. Are planners/adapters pure?
4. Are duplicate external reference keys handled exactly as before?
5. Are candidate `ExternalReferenceKeys` and `RecordedArtifactExpectationIds` updated consistently?
6. Did tests cover positive and negative cases for the migrated source family?
7. Are no Process Core or production driver API tokens present?
8. Are no UI/mobile proof artifacts present?
9. Are old unrelated failures documented separately?
