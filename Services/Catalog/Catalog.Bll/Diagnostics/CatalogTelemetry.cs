using System.Diagnostics;

namespace Catalog.Bll.Diagnostics;

/// <summary>Власні spans бізнес-операцій каталогу (ServiceDefaults: AddSource("Cinema.*")).</summary>
public static class CatalogTelemetry
{
    public const string SourceName = "Cinema.Catalog";
    public static readonly ActivitySource Source = new(SourceName);
}
