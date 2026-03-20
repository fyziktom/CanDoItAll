<?php
declare(strict_types=1);

use PHPUnit\Framework\TestCase;

final class RepositoryHashingTest extends TestCase
{
    public function testCanonicalJsonSortsObjectKeys(): void
    {
        $left = ['b' => 2, 'a' => 1];
        $right = ['a' => 1, 'b' => 2];

        $this->assertSame(
            RepositoryHashing::canonicalJson($left),
            RepositoryHashing::canonicalJson($right)
        );
    }

    public function testSha256HexIsStable(): void
    {
        $this->assertSame(
            RepositoryHashing::sha256Hex('hello'),
            RepositoryHashing::sha256Hex('hello')
        );
    }
}
