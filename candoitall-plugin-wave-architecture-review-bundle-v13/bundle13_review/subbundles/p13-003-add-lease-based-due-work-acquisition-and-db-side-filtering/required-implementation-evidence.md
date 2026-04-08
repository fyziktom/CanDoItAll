# Required implementation evidence

- automation dispatcher no longer materializes all deliveries before filtering,
- connector outbox no longer materializes all pending commands before filtering,
- automation delivery locking fields are used as an actual lease/claim boundary or replaced with a stronger mechanism,
- parallel worker instances can safely coexist.
