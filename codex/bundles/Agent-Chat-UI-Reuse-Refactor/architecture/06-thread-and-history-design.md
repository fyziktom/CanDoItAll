# Thread rail and history design

## Neutral owner

- rail header/count/status;
- search field and clear behavior;
- loading, empty, and error surfaces;
- selected thread item;
- preview/timestamp/meta/badges;
- new-thread/refresh action slots;
- bounded history dialog list and selection presentation.

## Agent owner

- loading workspace/session records;
- active Agent identity;
- creating a session;
- selecting a session;
- title persistence;
- pending approval counts;
- auto-approval policy;
- restoring/opening a history item;
- errors and notifications from Agent services.

## Compatibility invariants

- selection does not change because presentation records are recreated;
- stable keys are used for Razor `@key`;
- search behavior and ordering match the current implementation;
- current selected item remains visible;
- active-session and history badges remain;
- dialog close/select behavior remains;
- no unbounded transcript/session load is introduced.
