namespace PriceComparator.Application.Interfaces.Services;

public interface ICneAuthService
{
    Task<string> GetToken(CancellationToken cancellationToken);
}