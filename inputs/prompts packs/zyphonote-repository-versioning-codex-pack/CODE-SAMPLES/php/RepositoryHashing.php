<?php
declare(strict_types=1);

final class RepositoryHashing
{
    public static function sha256Hex(string $bytes): string
    {
        return strtolower(hash('sha256', $bytes));
    }

    public static function canonicalJson(mixed $value): string
    {
        $normalized = self::normalizeValue($value);
        $json = json_encode($normalized, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);

        if (!is_string($json)) {
            throw new RuntimeException('Failed to encode canonical JSON.');
        }

        return $json;
    }

    private static function normalizeValue(mixed $value): mixed
    {
        if (is_array($value)) {
            if (array_is_list($value)) {
                return array_map([self::class, 'normalizeValue'], $value);
            }

            $keys = array_keys($value);
            sort($keys, SORT_STRING);

            $result = [];
            foreach ($keys as $key) {
                $result[(string)$key] = self::normalizeValue($value[$key]);
            }

            return $result;
        }

        return $value;
    }
}
