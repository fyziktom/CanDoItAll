using CanDoItAll.Infrastructure;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionSourceRecoveryTests {
    [Fact]
    public void Captured_source_does_not_adopt_later_mutations_of_input_or_exported_path_record() {
        var profile = Guid.NewGuid();
        var root = BindRoot();
        var captured = new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(profile, root);
        var original = root.Path;
        root.Path = Path.Combine(root.Path, "replacement");
        root.HostBindingId = "replacement-host";
        var exported = captured.ToPathRecord();
        exported.Path = root.Path;
        exported.State = HostBoundPathState.Disabled;

        Assert.Equal(profile, captured.DatabaseProfileId);
        Assert.Equal(original, captured.RootPath);
        Assert.Equal(HostBoundPathState.Active, captured.ToPathRecord().State);
        Assert.Equal(original, captured.ToPathRecord().Path);
        captured.RequireCurrentHost();
    }

    [Fact]
    public void Unknown_version_inactive_root_and_syntax_mismatch_cannot_be_captured() {
        var root = BindRoot();
        root.FormatVersion++;
        Assert.Throws<ArgumentException>(() => new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(Guid.NewGuid(), root));
        root = BindRoot();
        root.State = HostBoundPathState.NeedsRebind;
        Assert.Throws<ArgumentException>(() => new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(Guid.NewGuid(), root));
        root = BindRoot();
        root.PathSyntax = PhysicalPathSyntax.Relative;
        Assert.Throws<ArgumentException>(() => new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(Guid.NewGuid(), root));
    }

    [Fact]
    public void Retained_foreign_host_identity_remains_inspectable_but_cannot_authorize_current_host_probe() {
        var root = BindRoot();
        root.HostBindingId = root.HostBindingId == "foreign-host-for-proof" ? "second-foreign-host" : "foreign-host-for-proof";
        var captured = new ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog(Guid.NewGuid(), root);
        Assert.Equal(root.HostBindingId, captured.HostBindingId);
        Assert.Throws<InvalidOperationException>(captured.RequireCurrentHost);
    }

    private static HostBoundPathRecord BindRoot()
        => HostBoundPathPolicy.BindCurrent(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "p03-source-contract", Guid.NewGuid().ToString("N"))),
            DateTimeOffset.UtcNow);
}
