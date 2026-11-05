using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class SubTypeRepository(PriceComparatorDbContext context)
    : Repository<Subtype, long>(context), ISubTypeRepository
{
    public async Task<Subtype?> GetOrCreateAsync(long conceptId, string name, CancellationToken cancellationToken = default)
    {
        name = Regex.Replace(name, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var subtypes = context.Set<Subtype>();

        // Primero revisamos los registros pendientes del DbContext
        var subtype = subtypes.Local.FirstOrDefault(x =>
            x.ConceptId == conceptId &&
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (subtype is not null)
            return subtype;

        // Después consultamos PostgreSQL
        subtype = await subtypes.FirstOrDefaultAsync(
            x => x.ConceptId == conceptId && x.Name.ToLower() == name.ToLower(),
            cancellationToken);

        if (subtype is not null)
            return subtype;

        // Si no existe, lo registramos
        subtype = new Subtype
        {
            Name = name,
            ConceptId = conceptId,
            Value = name.Replace(" ", "_")
        };

        await AddAsync(subtype, cancellationToken);

        return subtype;
    }
    
    public Subtype? Extract(string name, List<Subtype> subtypes)
    {
        foreach (var subtype in subtypes)
        {
            if (subtype.Terms.Any(term => name.Contains(term.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return subtype;
            }
        }

        return null;
    }
    
    public Subtype ExtractByName(Product product, Concept concept, Measurement? measurement, Packaging? packaging)
    {
        var name = product.Name;

        if (!string.IsNullOrWhiteSpace(product.Brand))
        {
            name = name.Replace(product.Brand, "", StringComparison.OrdinalIgnoreCase);
        }
        
        name = name.Replace(concept.Name, "", StringComparison.OrdinalIgnoreCase);
       
        if (!string.IsNullOrWhiteSpace(measurement?.DisplayName))
        {
            name = name.Replace(measurement.DisplayName, "", StringComparison.OrdinalIgnoreCase);
        }
        
        if (!string.IsNullOrWhiteSpace(measurement?.SecondDisplayName))
        {
            name = name.Replace(measurement.SecondDisplayName, "", StringComparison.OrdinalIgnoreCase);
            name = name.Replace("Pack", "", StringComparison.OrdinalIgnoreCase);
        }
        
        if (!string.IsNullOrWhiteSpace(packaging?.Name))
        {
            name = name.Replace(packaging.Terms[0].Name, "", StringComparison.OrdinalIgnoreCase);
        }
        
        name = Regex.Replace(name, @"\s+", " ").Trim();
        
        return new Subtype
        {
            Name = name,
            Value = name.Replace(" ", "_"),
            ConceptId = concept.Id
        };
    }

    
}