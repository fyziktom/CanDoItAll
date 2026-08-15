# Historical negative — unbounded and N+1 paths

The source guard inspected parent commit `c0bc6d0aee8f6b752bd4fb6b44663e7c2ee7a23b` using `git show`.
It intentionally failed when it found definition/conversation per-item read loops and transcript
pagination using in-memory `Skip(offset)` after document load.

| Command | Exit | Result |
|---|---:|---|
| Historical source guard against the three pre-SB05 owners | 1 | Expected red: `NEGATIVE CONFIRMED: prior head performs per-item definition/conversation reads and in-memory transcript Skip(offset).` |

The current-source guard passes because command repositories no longer expose those collection
operations, list services use read models, and transcript paging delegates to bounded SQL.
