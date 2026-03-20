<?php
declare(strict_types=1);

interface ScoreMergeServiceInterface
{
    public function compare(string $baseCanonicalJson, string $targetCanonicalJson): array;

    public function mergePreview(
        string $baseCanonicalJson,
        string $oursCanonicalJson,
        string $theirsCanonicalJson
    ): array;

    public function applyResolution(
        string $baseCanonicalJson,
        string $oursCanonicalJson,
        string $theirsCanonicalJson,
        array $resolutions
    ): array;
}
