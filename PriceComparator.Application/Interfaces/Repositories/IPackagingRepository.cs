using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;

public interface IPackagingRepository
    : IRepository<Packaging, long>
{
    Task<List<Packaging>> GetAllWithTermsAsync(CancellationToken cancellationToken = default);
    Packaging? Extract(string name, IReadOnlyCollection<Packaging> packagings);
}