using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class PackagingRepository(
    PriceComparatorDbContext context)
    : Repository<Packaging, long>(context), 
        IPackagingRepository
{
    public async Task<List<Packaging>> GetAllWithTermsAsync(CancellationToken cancellationToken = default)
    {
        return await Entities
            .AsNoTracking()
            .Include(x => x.Terms)
            .ThenInclude(t => t.Aliases)
            .ToListAsync(cancellationToken);
    }
    
    public Packaging? Extract(string name, IReadOnlyCollection<Packaging> packagings, string? slug)
    {
        var normalizedName = Normalize(name);
        var normalizedSlug = Normalize(slug ?? string.Empty);
        
        foreach (var packaging in packagings)
        {
            var term = packaging.Terms
                .SelectMany(t => new[] { t.Name }
                    .Concat(t.Aliases.Select(a => a.Name))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => new
                    {
                        Term = t,
                        Value = Normalize(value)
                    }))
                .Where(x =>
                {
                    var pattern = $@"(?<!\w){Regex.Escape(x.Value)}(?!\w)";

                    return Regex.IsMatch(normalizedName, pattern, RegexOptions.IgnoreCase) ||
                           Regex.IsMatch(normalizedSlug, pattern, RegexOptions.IgnoreCase);
                })
                .OrderByDescending(x => x.Value.Length)
                .Select(x => x.Term)
                .FirstOrDefault();
            
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
    
    private static string Normalize(string value)
    {
        return Regex.Replace(
            value.Replace("-", " ").Replace("/", " ").Trim(),
            @"\s+",
            " ");
    }

}
        
        
 