## Stop conditions

Stop and redesign instead of pushing forward if any of these happen during implementation:

- the refactor tries to keep both hierarchy truths “for compatibility” in the active path
- plugin-first UI cannot be achieved without inventing new legacy enum members
- foreign-owner IDs start moving from one metadata helper to another instead of leaving the writable metadata contract
- binding data is kept on node core “temporarily” with no migration/removal plan
- external connector side effects are added before the durable operation boundary exists
