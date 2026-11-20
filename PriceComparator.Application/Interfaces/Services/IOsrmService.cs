
using PriceComparator.Application.DTOs;

namespace PriceComparator.Application.Interfaces.Services;

public interface IOsrmService
{
    Task<OsrmResponseDto> GetRouteAsync(IReadOnlyList<OsrmRequestDto> points, CancellationToken cancellationToken);
}