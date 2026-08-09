using CanDoItAll.Infrastructure;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class MigrationBackupIntegrityTests
{
    [Fact]
    public void CreateOrVerify_rejects_a_preexisting_backup_from_a_different_source()
    {
        string root = TestFileSystem.CreateTemporaryRoot("migration-backup-stale");
        try
        {
            string backupPath = Path.Combine(root, "migration.v1.backup.json");
            var writer = new DurableFileWriter(TestWorkspaceServices.PhysicalPathPolicyFactory);
            MigrationBackupIntegrity.CreateOrVerify(writer, root, backupPath, "{\"version\":1}");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MigrationBackupIntegrity.CreateOrVerify(
                    writer,
                    root,
                    backupPath,
                    "{\"version\":1,\"changed\":true}"));

            Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void ReadVerified_rejects_a_backup_without_its_integrity_manifest()
    {
        string root = TestFileSystem.CreateTemporaryRoot("migration-backup-manifest");
        try
        {
            string backupPath = Path.Combine(root, "migration.v1.backup.json");
            File.WriteAllText(backupPath, "{\"version\":1}");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MigrationBackupIntegrity.ReadVerified(backupPath));

            Assert.Contains("manifest", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }
}
