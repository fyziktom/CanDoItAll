<?php
declare(strict_types=1);

interface RepositoryServiceInterface
{
    public function createRepository(
        string $ownerUserId,
        string $entityType,
        string $rootEntityId,
        string $defaultBranchName = 'main'
    ): array;

    public function createCommit(
        string $repositoryId,
        string $branchName,
        ?string $expectedTipCommitHash,
        string $authorUserId,
        string $authorName,
        string $message,
        array $files,
        array $metadata = []
    ): array;

    public function createBranch(
        string $repositoryId,
        string $branchName,
        ?string $fromCommitHash,
        string $actorUserId
    ): array;

    public function mergeBranch(
        string $repositoryId,
        string $sourceBranchName,
        string $targetBranchName,
        ?string $expectedTargetTipCommitHash,
        string $actorUserId,
        string $message
    ): array;

    public function forkRepository(
        string $sourceRepositoryId,
        string $targetOwnerUserId,
        ?string $sourceBranchName,
        string $actorUserId
    ): array;
}
