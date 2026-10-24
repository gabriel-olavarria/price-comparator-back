
namespace PriceComparator.Application.Interfaces.Services;

public interface ITurnstileVerificationService
{
    Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken = default);
}