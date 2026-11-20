public sealed class RouteDto
{
    public double TotalDistanceKm { get; init; }

    public List<RouteSegmentDto> Segments { get; init; } = [];
}

public sealed class RouteSegmentDto
{
    public string From { get; init; } = string.Empty;

    public string To { get; init; } = string.Empty;

    public double DistanceKm { get; init; }
}