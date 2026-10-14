using PriceComparator.Domain.Common;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Services;

public static class ProductConceptDetector
{
    public static Concept? DetectFromText(string? value, IReadOnlyCollection<Concept> concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // 1. FRASES
        // En la consulta, las frases tienen prioridad absoluta
        // sobre los aliases.
        var phraseMatches = FindMatches(value, concepts, concept => concept.Phrases, isPhrase: true);

        if (phraseMatches.Count > 0)
        {
            return SelectBestMatch(phraseMatches).Concept;
        }

        // 2. ALIASES
        var aliasMatches = FindMatches(value, concepts, concept => concept.Aliases, isPhrase: false);

        return aliasMatches.Count > 0 ? SelectBestMatch(aliasMatches).Concept : null;
    }
    
    public static Product DetectClasification(Product product, Concept queryConcept)
    {
        var measurement = ProductExtractMeasurement.Extract(product.Name);
        var subtype = ProductExtractSubType.Extract(product.Name, queryConcept.Subtypes);
        
        product.Clasification = new Clasification
        {
            Id = queryConcept.Id,
            Category = queryConcept.Category,
            Subtype = subtype,
            Measurement = measurement
        };

        if (subtype is not null)
        {
            product.CanonicalKey =
                $"{product.Clasification.Category}_" +
                $"{product.Clasification.Id}_" +
                $"{product.Clasification.Subtype}_" +
                $"{product.Clasification.Measurement.QuantityUnit}{product.Clasification.Measurement.Unit}";
        }
        
        return product;
    }
    
    private static List<ConceptMatch> FindMatches(string value, IReadOnlyCollection<Concept> concepts, Func<Concept, IReadOnlyCollection<string>> termsSelector, bool isPhrase)
    {
        var matches = new List<ConceptMatch>();

        foreach (var concept in concepts)
        {
            foreach (var term in termsSelector(concept))
            {
                var index = ProductNormalizer.GetTermIndex(value, term);

                if (index < 0)
                {
                    continue;
                }

                matches.Add(new ConceptMatch(concept, term, index, isPhrase));
            }
        }
        return matches;
    }

    private static ConceptMatch SelectBestMatch(IEnumerable<ConceptMatch> matches)
    {
        return matches
            .OrderBy(match => match.Index)
            .ThenByDescending(match => ProductNormalizer.Normalize(match.Term).Length)
            .ThenByDescending(match => match.IsPhrase)
            .First();
    }

    private sealed record ConceptMatch(Concept Concept, string Term, int Index, bool IsPhrase);
}