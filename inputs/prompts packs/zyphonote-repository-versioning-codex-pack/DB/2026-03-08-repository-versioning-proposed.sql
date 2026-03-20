-- Proposed additive migration for Zyphonote repository versioning
-- PHP 8.2 + MariaDB
-- This is intentionally written as a concrete starting point, not as a promise that every column name is final.

SET NAMES utf8mb4;
SET time_zone = '+00:00';

CREATE TABLE IF NOT EXISTS vcs_repositories (
  id CHAR(36) NOT NULL PRIMARY KEY,
  owner_user_id CHAR(36) NOT NULL,
  entity_type ENUM('score', 'learning_package', 'playlist', 'event') NOT NULL,
  root_entity_id CHAR(36) NOT NULL,
  default_branch_name VARCHAR(120) NOT NULL DEFAULT 'main',
  upstream_repository_id CHAR(36) NULL,
  fork_policy ENUM('disabled', 'same_owner_only', 'public') NOT NULL DEFAULT 'same_owner_only',
  visibility ENUM('private', 'unlisted', 'public') NOT NULL DEFAULT 'private',
  allow_merge_requests TINYINT(1) NOT NULL DEFAULT 1,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  created_utc DATETIME(6) NOT NULL,
  updated_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_vcs_repositories_owner FOREIGN KEY (owner_user_id) REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_repositories_upstream FOREIGN KEY (upstream_repository_id) REFERENCES vcs_repositories(id) ON DELETE SET NULL,
  KEY ix_vcs_repositories_entity (entity_type, root_entity_id),
  KEY ix_vcs_repositories_owner (owner_user_id),
  KEY ix_vcs_repositories_upstream (upstream_repository_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_blobs (
  sha256 CHAR(64) NOT NULL PRIMARY KEY,
  cid_v1_raw VARCHAR(90) NULL,
  storage_key VARCHAR(255) NOT NULL,
  size_bytes BIGINT UNSIGNED NOT NULL DEFAULT 0,
  mime_type VARCHAR(120) NOT NULL DEFAULT 'application/octet-stream',
  content_kind VARCHAR(64) NOT NULL DEFAULT 'raw',
  created_utc DATETIME(6) NOT NULL,
  UNIQUE KEY uq_vcs_blobs_storage_key (storage_key),
  KEY ix_vcs_blobs_cid (cid_v1_raw)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_snapshots (
  sha256 CHAR(64) NOT NULL PRIMARY KEY,
  cid_v1_raw VARCHAR(90) NULL,
  storage_key VARCHAR(255) NOT NULL,
  manifest_size_bytes BIGINT UNSIGNED NOT NULL DEFAULT 0,
  entry_count INT UNSIGNED NOT NULL DEFAULT 0,
  created_utc DATETIME(6) NOT NULL,
  UNIQUE KEY uq_vcs_snapshots_storage_key (storage_key),
  KEY ix_vcs_snapshots_cid (cid_v1_raw)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_snapshot_entries (
  snapshot_sha256 CHAR(64) NOT NULL,
  path VARCHAR(255) NOT NULL,
  blob_sha256 CHAR(64) NOT NULL,
  file_mode VARCHAR(16) NOT NULL DEFAULT '100644',
  ordinal INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (snapshot_sha256, path),
  CONSTRAINT fk_vcs_snapshot_entries_snapshot FOREIGN KEY (snapshot_sha256) REFERENCES vcs_snapshots(sha256) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_snapshot_entries_blob FOREIGN KEY (blob_sha256) REFERENCES vcs_blobs(sha256) ON DELETE RESTRICT,
  KEY ix_vcs_snapshot_entries_blob (blob_sha256)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_commits (
  id CHAR(36) NOT NULL PRIMARY KEY,
  repository_id CHAR(36) NOT NULL,
  commit_sha256 CHAR(64) NOT NULL,
  snapshot_sha256 CHAR(64) NOT NULL,
  payload_storage_key VARCHAR(255) NOT NULL,
  commit_kind ENUM('commit', 'merge', 'import', 'publish', 'sync', 'backfill', 'system') NOT NULL DEFAULT 'commit',
  message VARCHAR(500) NOT NULL,
  author_user_id CHAR(36) NULL,
  author_name VARCHAR(180) NULL,
  authored_utc DATETIME(6) NOT NULL,
  committed_utc DATETIME(6) NOT NULL,
  metadata_json MEDIUMTEXT NOT NULL,
  created_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_vcs_commits_repository FOREIGN KEY (repository_id) REFERENCES vcs_repositories(id) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_commits_snapshot FOREIGN KEY (snapshot_sha256) REFERENCES vcs_snapshots(sha256) ON DELETE RESTRICT,
  CONSTRAINT fk_vcs_commits_author FOREIGN KEY (author_user_id) REFERENCES users(id) ON DELETE SET NULL,
  UNIQUE KEY uq_vcs_commits_hash (commit_sha256),
  KEY ix_vcs_commits_repository_committed (repository_id, committed_utc),
  KEY ix_vcs_commits_author (author_user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_commit_parents (
  commit_sha256 CHAR(64) NOT NULL,
  parent_commit_sha256 CHAR(64) NOT NULL,
  sort_order TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (commit_sha256, parent_commit_sha256),
  CONSTRAINT fk_vcs_commit_parents_commit FOREIGN KEY (commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_commit_parents_parent FOREIGN KEY (parent_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE RESTRICT,
  KEY ix_vcs_commit_parents_parent (parent_commit_sha256)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_refs (
  id CHAR(36) NOT NULL PRIMARY KEY,
  repository_id CHAR(36) NOT NULL,
  ref_type ENUM('branch', 'tag', 'published') NOT NULL DEFAULT 'branch',
  name VARCHAR(255) NOT NULL,
  tip_commit_sha256 CHAR(64) NULL,
  base_commit_sha256 CHAR(64) NULL,
  is_default TINYINT(1) NOT NULL DEFAULT 0,
  is_protected TINYINT(1) NOT NULL DEFAULT 0,
  is_deleted TINYINT(1) NOT NULL DEFAULT 0,
  created_by_user_id CHAR(36) NULL,
  created_utc DATETIME(6) NOT NULL,
  updated_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_vcs_refs_repository FOREIGN KEY (repository_id) REFERENCES vcs_repositories(id) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_refs_tip FOREIGN KEY (tip_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE SET NULL,
  CONSTRAINT fk_vcs_refs_base FOREIGN KEY (base_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE SET NULL,
  CONSTRAINT fk_vcs_refs_creator FOREIGN KEY (created_by_user_id) REFERENCES users(id) ON DELETE SET NULL,
  UNIQUE KEY uq_vcs_refs_repository_name (repository_id, ref_type, name),
  KEY ix_vcs_refs_tip (tip_commit_sha256)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS vcs_merge_requests (
  id CHAR(36) NOT NULL PRIMARY KEY,
  source_repository_id CHAR(36) NOT NULL,
  source_branch_name VARCHAR(255) NOT NULL,
  source_head_commit_sha256 CHAR(64) NOT NULL,
  target_repository_id CHAR(36) NOT NULL,
  target_branch_name VARCHAR(255) NOT NULL,
  target_head_commit_sha256 CHAR(64) NOT NULL,
  merge_base_commit_sha256 CHAR(64) NULL,
  title VARCHAR(255) NOT NULL,
  description MEDIUMTEXT NULL,
  status ENUM('draft', 'open', 'closed', 'merged') NOT NULL DEFAULT 'open',
  mergeable_state ENUM('unknown', 'clean', 'conflicts', 'behind', 'blocked', 'merged') NOT NULL DEFAULT 'unknown',
  merge_strategy ENUM('merge_commit', 'fast_forward') NOT NULL DEFAULT 'merge_commit',
  created_by_user_id CHAR(36) NOT NULL,
  created_utc DATETIME(6) NOT NULL,
  updated_utc DATETIME(6) NOT NULL,
  merged_utc DATETIME(6) NULL,
  merged_by_user_id CHAR(36) NULL,
  merged_commit_sha256 CHAR(64) NULL,
  CONSTRAINT fk_vcs_mr_source_repo FOREIGN KEY (source_repository_id) REFERENCES vcs_repositories(id) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_mr_target_repo FOREIGN KEY (target_repository_id) REFERENCES vcs_repositories(id) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_mr_source_head FOREIGN KEY (source_head_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE RESTRICT,
  CONSTRAINT fk_vcs_mr_target_head FOREIGN KEY (target_head_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE RESTRICT,
  CONSTRAINT fk_vcs_mr_merge_base FOREIGN KEY (merge_base_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE SET NULL,
  CONSTRAINT fk_vcs_mr_creator FOREIGN KEY (created_by_user_id) REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_vcs_mr_merged_by FOREIGN KEY (merged_by_user_id) REFERENCES users(id) ON DELETE SET NULL,
  CONSTRAINT fk_vcs_mr_merged_commit FOREIGN KEY (merged_commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE SET NULL,
  KEY ix_vcs_mr_target (target_repository_id, target_branch_name, status),
  KEY ix_vcs_mr_source (source_repository_id, source_branch_name, status),
  KEY ix_vcs_mr_creator (created_by_user_id, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE scores
  ADD COLUMN IF NOT EXISTS repository_id CHAR(36) NULL,
  ADD COLUMN IF NOT EXISTS current_commit_sha256 CHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS published_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_scores_repository (repository_id),
  ADD KEY IF NOT EXISTS ix_scores_current_commit (current_commit_sha256);

ALTER TABLE learning_packages
  ADD COLUMN IF NOT EXISTS repository_id CHAR(36) NULL,
  ADD COLUMN IF NOT EXISTS current_commit_sha256 CHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS published_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_learning_packages_repository (repository_id),
  ADD KEY IF NOT EXISTS ix_learning_packages_current_commit (current_commit_sha256);

ALTER TABLE playlist_plans
  ADD COLUMN IF NOT EXISTS repository_id CHAR(36) NULL,
  ADD COLUMN IF NOT EXISTS current_commit_sha256 CHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS shared_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_playlist_plans_repository (repository_id),
  ADD KEY IF NOT EXISTS ix_playlist_plans_current_commit (current_commit_sha256);

ALTER TABLE performance_events
  ADD COLUMN IF NOT EXISTS repository_id CHAR(36) NULL,
  ADD COLUMN IF NOT EXISTS current_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_performance_events_repository (repository_id),
  ADD KEY IF NOT EXISTS ix_performance_events_current_commit (current_commit_sha256);

ALTER TABLE score_versions
  ADD COLUMN IF NOT EXISTS commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_score_versions_commit (commit_sha256);

ALTER TABLE learning_package_versions
  ADD COLUMN IF NOT EXISTS commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_learning_package_versions_commit (commit_sha256);

ALTER TABLE playlist_plan_versions
  ADD COLUMN IF NOT EXISTS commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_playlist_plan_versions_commit (commit_sha256);

ALTER TABLE playlist_plan_shares
  ADD COLUMN IF NOT EXISTS shared_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_playlist_plan_shares_commit (shared_commit_sha256);

ALTER TABLE score_purchases
  ADD COLUMN IF NOT EXISTS purchased_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_score_purchases_commit (purchased_commit_sha256);

ALTER TABLE learning_package_purchases
  ADD COLUMN IF NOT EXISTS purchased_commit_sha256 CHAR(64) NULL,
  ADD KEY IF NOT EXISTS ix_learning_package_purchases_commit (purchased_commit_sha256);

-- Optional new historical bridge for events if you want explicit legacy-style rows.
CREATE TABLE IF NOT EXISTS performance_event_versions (
  id CHAR(36) NOT NULL PRIMARY KEY,
  event_id CHAR(36) NOT NULL,
  version_no INT UNSIGNED NOT NULL,
  commit_sha256 CHAR(64) NOT NULL,
  source_kind ENUM('commit', 'merge', 'backfill') NOT NULL DEFAULT 'commit',
  change_note VARCHAR(255) NULL,
  created_utc DATETIME(6) NOT NULL,
  created_by_user_id CHAR(36) NULL,
  CONSTRAINT fk_performance_event_versions_event FOREIGN KEY (event_id) REFERENCES performance_events(id) ON DELETE CASCADE,
  CONSTRAINT fk_performance_event_versions_commit FOREIGN KEY (commit_sha256) REFERENCES vcs_commits(commit_sha256) ON DELETE RESTRICT,
  CONSTRAINT fk_performance_event_versions_creator FOREIGN KEY (created_by_user_id) REFERENCES users(id) ON DELETE SET NULL,
  UNIQUE KEY uq_performance_event_versions_event_no (event_id, version_no),
  KEY ix_performance_event_versions_commit (commit_sha256)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
