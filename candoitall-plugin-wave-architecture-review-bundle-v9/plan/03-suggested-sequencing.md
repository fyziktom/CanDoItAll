# Suggested sequencing
1. P9-001 + P9-002 + P9-007 together (carrier retirement and removal of read-time normalization)
2. P9-003 (marker single truth)
3. P9-004 + P9-005 together (plugin-first editor + identity)
4. P9-006 (extensible node references)
5. P9-008 (durable connector boundary)

Do not start the write-side plugin wave before steps 1–4 are closed, and do not ship write-side connectors before step 5 is closed.
