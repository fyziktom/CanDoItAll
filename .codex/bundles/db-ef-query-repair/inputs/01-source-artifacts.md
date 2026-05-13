# Source Artifacts

- `C:\repositories\CanDoItAll`
- `C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md`
- `C:\Users\lucys\.codex\skills\optimizing-ef-core-queries\SKILL.md`
- Shell scan: `git grep -n -I -E "ToListAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|CountAsync|AnyAsync|Include|AsNoTracking|DbContext" -- src tests tools`
- Shell scan: PowerShell windowed search for `.ToListAsync()` followed by in-memory `.OrderBy*()` or `.Take()`

