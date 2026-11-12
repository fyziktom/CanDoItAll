namespace CanDoItAll.Tests.Playwright.StorageCatalog;

public enum StorageCatalogProbeCommand { DenyApi, AllowApi, Stop }

public static class StorageCatalogProbeProtocol {
    public const string Ready = "STORAGE-CATALOG-PROBE-READY";
    public static string Ack(StorageCatalogProbeCommand command) => $"STORAGE-CATALOG-PROBE-ACK:{command}";
}
