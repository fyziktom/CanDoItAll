# Prompt: remote files and bundle tools

Implementuj:
- `fs_apply_bundle`
- `fs_read_text`
- `fs_backup_path`
- `fs_restore_backup`

Požadavky:
- allow-listed roots,
- path normalization,
- atomic deployment pattern přes staging dir + rename/symlink swap,
- revision metadata a audit trail,
- ochrana proti traversal a symlink bypass.

Přidej:
- unit testy path guardu,
- integration testy bundle apply + restore.
