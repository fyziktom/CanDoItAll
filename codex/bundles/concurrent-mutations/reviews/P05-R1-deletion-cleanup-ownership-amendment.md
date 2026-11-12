# Deletion cleanup ownership correction

This supersedes the finite-disposal wording in work card 86A6A651 and release
addendum 0CD74C8C without changing their retained bytes. Early source review found
that timing out a `WaitAsync` around a noncancelable EF/source disposal would leave
the underlying operation running while later rollback/close/context disposal began.
The parent selected terminal, single-invocation ownership instead of a detached
cleanup registry or another runtime coordination layer.

Admission retains one five-second cancellation budget. SQL cleanup has an independent
five-second cancellation/command budget. Every noncancelable operation is awaited to
terminal before another connection operation begins. Source and explicitly owned
transaction/context disposal have one invocation; physical close may have sequential
quarantine or EF open-reference balancing attempts after terminal failure. There is
one in-flight cleanup owner, never a timed-out waiter racing the underlying task.
Cleanup is cooperative and has no claimed hard wall-clock upper bound. The
outer test-process deadline remains the independent escape and resource-reconciliation
boundary. Terminal cleanup failure retains acknowledged commit knowledge and skips
independent completion callbacks. An uncertain session is quarantined before pool
reuse where ownership permits; no second operation runs concurrently with a pending
cleanup task. Caller retry suppression from the preceding release remains mandatory.

This records a source-design correction, not runtime validation.
