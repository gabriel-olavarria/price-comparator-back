using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class PackagingRepository(PriceComparatorDbContext context)
    : Repository<Packaging, long>(context), IPackagingRepository
{
    public async Task<List<Packaging>> GetAllWithTermsAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<Packaging>()
            .AsNoTracking()
            .Include(x => x.Terms)
            .ToListAsync(cancellationToken);
    }
    
    public Packaging? Extract(string name, IReadOnlyCollection<Packaging> packagings)
    {
        foreach (var packaging in packagings)
        {
            var term = packaging.Terms.FirstOrDefault(term =>
                !string.IsNullOrWhiteSpace(term.Name) &&
                Regex.IsMatch(name, $@"(?<!\w){Regex.Escape(term.Name)}(?!\w)", RegexOptions.IgnoreCase));

            if (term is null)
                continue;
            
            return new Packaging
            {
                Id = packaging.Id,
                Name = packaging.Name,
                Terms = [term]
            };
        }

        return null;
    }

}
        
        
 