using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderHistoryPolicyTests {
    [Theory]
    [InlineData(0, 1, 1, 1, 1)]
    [InlineData(3651, 1, 1, 1, 1)]
    [InlineData(30, 31, 1, 1, 1)]
    [InlineData(30, 0, 1, 1, 1)]
    [InlineData(30, 1, 0, 1, 1)]
    [InlineData(30, 1, 131073, 1, 1)]
    [InlineData(30, 1, 1, 0, 1)]
    [InlineData(30, 1, 1, 1, 1001)]
    public void Policy_rejects_invalid_retention_bounds_and_quota(int metadataDays, int detailDays, int textBytes, long quota, int batch) {
        var policy = new HistoryPolicy {
            MetadataRetentionDays = metadataDays, DetailRetentionDays = detailDays,
            MaximumTextBytes = textBytes, DetailQuotaBytes = quota, BatchSize = batch
        };
        Assert.Throws<ArgumentException>(() => HistoryContractValidation.Validate(policy));
    }

    [Fact]
    public void Policy_rejects_unknown_capture_mode() {
        Assert.Throws<ArgumentException>(() => HistoryContractValidation.Validate(new HistoryPolicy { CaptureMode = (HistoryCaptureMode)100 }));
    }

    [Fact]
    public void Policy_accepts_explicit_supported_bounds() {
        HistoryContractValidation.Validate(new HistoryPolicy {
            MetadataRetentionDays = 3650, DetailRetentionDays = 3650,
            MaximumTextBytes = 131072, DetailQuotaBytes = 1, BatchSize = 1000
        });
    }
}
