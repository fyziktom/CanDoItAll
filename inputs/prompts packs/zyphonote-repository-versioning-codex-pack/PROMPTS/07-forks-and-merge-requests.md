Finish forks and merge requests end-to-end.

Required work:
1. Implement fork policy defaults and validations.
2. Implement fork creation from repository + branch.
3. Implement MR create/list/detail/close/merge.
4. Implement mergeability revalidation at merge time.
5. Block unsafe publication/listing flows for non-owner forks unless rights policy allows it.

Important:
- private event/playlist repos should not become casually public-forkable
- commercial/public score/package forks need rights-aware restrictions

Update checklists after completion.
