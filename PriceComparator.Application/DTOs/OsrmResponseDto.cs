namespace PriceComparator.Application.DTOs;

public sealed class OsrmResponseDto
{
    public string? Code { get; init; }
    public List<OsrmTripDto> Trips { get; init; } = [];
    public List<OsrmWaypointDto> Waypoints { get; init; } = [];
}

public sealed class OsrmTripDto
{
    public double Distance { get; init; }
    public double Duration { get; init; }
    public List<OsrmLegDto> Legs { get; init; } = [];
}

public sealed class OsrmLegDto
{
    public double Distance { get; init; }
    public double Duration { get; init; }
}

public sealed class OsrmWaypointDto
{
    public int WaypointIndex { get; init; }
}