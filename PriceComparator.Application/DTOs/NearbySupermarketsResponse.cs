
namespace PriceComparator.Application.DTOs;

public sealed class NearbySupermarketsResponse
{
    public IReadOnlyList<FeatureResultDto> NearestByStore { get; init; } = [];
    public RouteDto RouteResult { get; init; }
}
