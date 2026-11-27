using System.Diagnostics;

namespace Reviews.Application.Common.Diagnostics;

/// <summary>Власні spans сценаріїв відгуків (ServiceDefaults: AddSource("Cinema.*")).</summary>
public static class ReviewsTelemetry
{
    public const string SourceName = "Cinema.Reviews";
    public static readonly ActivitySource Source = new(SourceName);
}
