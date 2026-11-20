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

    public Subtype Extract(Product product, List<Subtype> subtypes, Concept concept, Measurement? measurement, Packaging? packaging)
    {
        Subtype subtype;
        
        foreach (var s in subtypes)
        {
            if (s.Terms.Any(term => product.Name.Contains(term.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return s;
            }
        }
        
        subtype = ExtractByName(product, concept, measurement, packaging);
        return subtype;
    }
    
    private Subtype ExtractByName(Product product, Concept concept, Measurement? measurement, Packaging? packaging)
    {
        var name = product.Name;
        
        //Quitar la marca
        if (!string.IsNullOrWhiteSpace(product.Brand))
        {
            name = name.Replace(product.Brand, "", StringComparison.OrdinalIgnoreCase);
        }
        //Quitar concepto
        if (!string.IsNullOrWhiteSpace(concept.Name))
        {
            name = name.Replace(concept.Name, "", StringComparison.OrdinalIgnoreCase);
        }
        //Quitar medida
        if (!string.IsNullOrWhiteSpace(measurement?.DisplayReplace))
        {
            name = name.Replace(measurement.DisplayReplace, "", StringComparison.OrdinalIgnoreCase);
            name = name.Replace("Pack", "", StringComparison.OrdinalIgnoreCase);
        }
        //Quitar envase
        if (!string.IsNullOrWhiteSpace(packaging?.Name))
        {
            name = name.Replace(packaging.Terms[0].Name, "", StringComparison.OrdinalIgnoreCase);
        }
        //Quitar espacios
        name = Regex.Replace(name, @"\s+", " ").Trim();
        
        return new Subtype
        {
            Name = name,
            Value = name.Replace(" ", "_"),
            ConceptId = concept.Id
        };
    }

    
}