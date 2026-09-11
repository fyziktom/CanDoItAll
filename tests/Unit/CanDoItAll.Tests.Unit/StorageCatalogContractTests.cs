using System.Reflection;
using System.Text.Json;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageCatalogContractTests {
    [Fact]
    public void Public_catalog_and_driver_contracts_exclude_mapped_records_and_raw_configuration() {
        Type[] contracts = [typeof(IStorageCatalogService), typeof(IStorageDriver), typeof(IStorageBrowseDriver),
            typeof(IStorageStablePlacementDriver), typeof(IStorageCatalogPathMigrationService)];
        foreach (var contract in contracts) {
            foreach (var method in contract.GetMethods()) {
                AssertNoMappedType(method.ReturnType);
                foreach (var parameter in method.GetParameters()) {
                    AssertNoMappedType(parameter.ParameterType);
                }
            }
        }
        Type[] snapshots = [typeof(StorageCatalogSnapshot), typeof(StorageRoutingRuleSnapshot), typeof(StorageDriverInput)];
        foreach (var type in snapshots) {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name is nameof(StorageCatalogRecord.ConfigJson) or nameof(StorageRoutingRule.AlternativeStorageIdsJson));
        }
        var record = new StorageCatalogRecord { ConfigJson = "{\"username\":\"private-value\",\"unknown\":\"opaque-only\"}" };
        var driver = record.ToDriverInput();
        Assert.DoesNotContain("private-value", JsonSerializer.Serialize(driver), StringComparison.Ordinal);
        Assert.DoesNotContain("opaque-only", driver.ToString(), StringComparison.Ordinal);
        Assert.Equal("private-value", driver.ReadConfiguration().Username);
    }

    [Fact]
    public void Exact_legacy_source_and_browse_fingerprints_use_original_configuration_bytes() {
        var record = new StorageCatalogRecord {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), ProviderKind = StorageProviderKind.Ftp,
            IsEnabled = true, IsReadOnly = false, CapabilityMask = StorageCapability.Read | StorageCapability.Write,
            ConnectionMode = StorageConnectionMode.Remote, EndpointOrRoot = "ftp://files.example.test/root",
            CredentialSecretId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ConfigJson = " {\n  \"port\": 2121, \"unknown\": \"retain\"\n } "
        };
        var driver = record.ToDriverInput();
        Assert.Equal("629b2682e64d084a2da55d1cb9c70142c66cbd93311dc56ae3140ae981191e89", driver.SourceFingerprint);
        Assert.Equal("acab396bc1fc5fd0f8a84a8b1732aca2f2f37215055ffe06807c66b7a5584ae1", driver.BuildBrowseFingerprint("reports"));
        Assert.Equal(2121, driver.ReadConfiguration().Port);
        record.ConfigJson = "{\"port\":2121,\"unknown\":\"retain\"}";
        Assert.NotEqual(driver.SourceFingerprint, record.ToSnapshot().SourceFingerprint);
        Assert.NotEqual(driver.BuildBrowseFingerprint("reports"), record.ToDriverInput().BuildBrowseFingerprint("reports"));
    }

    [Fact]
    public void Detached_driver_input_does_not_parse_configuration_until_the_selected_operation_reads_it() {
        var record = new StorageCatalogRecord { ProviderKind = StorageProviderKind.Ftp, ConfigJson = "{unparseable" };
        var metadata = record.ToSnapshot();
        var driver = record.ToDriverInput();
        Assert.Equal(record.Id, metadata.Id);
        Assert.NotEmpty(driver.SourceFingerprint);
        Assert.NotEmpty(driver.BuildBrowseFingerprint("root"));
        Assert.Throws<JsonException>(() => driver.ReadConfiguration());
        Assert.Null(StorageCatalogSaveRequest.FromSnapshot(metadata).Configuration);
        Assert.Null(StorageRoutingRuleSaveRequest.FromSnapshot(new StorageRoutingRule().ToSnapshot()).AlternativeStorageIds);
    }

    private static void AssertNoMappedType(Type type) {
        Assert.NotEqual(typeof(StorageCatalogRecord), type);
        Assert.NotEqual(typeof(StorageRoutingRule), type);
        foreach (var argument in type.GetGenericArguments()) {
            AssertNoMappedType(argument);
        }
    }
}
