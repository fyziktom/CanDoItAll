<?php
declare(strict_types=1);

final class BranchFastForwardGuard
{
    public static function assertExpectedTip(?string $currentTipCommitHash, ?string $expectedTipCommitHash): void
    {
        $current = self::normalizeHash($currentTipCommitHash);
        $expected = self::normalizeHash($expectedTipCommitHash);

        if ($current !== $expected) {
            throw new RuntimeException('Branch tip changed on origin. Fetch and retry.');
        }
    }

    private static function normalizeHash(?string $value): ?string
    {
        $trimmed = $value !== null ? trim($value) : '';
        return $trimmed !== '' ? strtolower($trimmed) : null;
    }
}
