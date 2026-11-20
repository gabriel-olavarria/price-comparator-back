using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface IPackagingRepository : IRepository<Packaging, long>
{
    Task<List<Packaging>> GetAllWithTermsAsync(CancellationToken cancellationToken = default);
    Packaging? Extract(string name, IReadOnlyCollection<Packaging> packagings, string? slug);
}