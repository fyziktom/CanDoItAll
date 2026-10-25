# B1 lifecycle wait observation repair

Status: source-only repair; the parent's failed 9/10 run remains retained. No repaired execution is claimed.

`Lifecycle_exclusion_and_binding_validation_precede_payload_reads` polled
`pg_stat_activity` inside the transaction already holding the lifecycle row.
[PostgreSQL 18 documents](https://www.postgresql.org/docs/18/monitoring-stats.html)
that current-query information is captured on its first use in a transaction and
then reused. A first poll before the capture became blocked could therefore stay
stale until the two-second observation deadline. This explains why the same source
passed previously and later missed the wait; increasing that timeout would not
repair the observation.

The test interceptor now captures the actual capture connection's backend PID at
the existing deadline-configuration command. The holder then polls
[pg_blocking_pids](https://www.postgresql.org/docs/18/functions-info.html) for that
exact backend and requires its own PID in the returned blockers. This reads live
lock-manager state, avoids query-text matching, retains the two-second bounded
poll and ten-millisecond delay, and leaves the lifecycle binding-change and zero
payload-read assertions unchanged. No production source, lock order, deadline or
test count changes: the class still contains exactly ten Facts.

Transfer only this test file and this review note; the concurrent neutral epoch
extraction in the managed checkout is unrelated and remains unvalidated.
