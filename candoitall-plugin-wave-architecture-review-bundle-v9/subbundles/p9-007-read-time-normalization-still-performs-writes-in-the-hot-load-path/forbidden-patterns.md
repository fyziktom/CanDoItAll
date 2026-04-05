# Forbidden patterns
- LoadAsync still calls NormalizeAndHydrateAsync that can persist changes
- NormalizeAndHydrateAsync still calls SaveChangesAsync in active load paths
- Compatibility cleanup still runs on every read
