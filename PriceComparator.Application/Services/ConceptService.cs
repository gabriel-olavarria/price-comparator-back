using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Application.Models;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Services;

namespace PriceComparator.Application.Services;

public class ConceptService  : IConceptService, ISingletonDependency
{
    
    public Concept? DetectFromText(string value, IReadOnlyCollection<Concept> concepts)
    {

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
    
    private List<ConceptMatch> FindMatches(string value, IReadOnlyCollection<Concept> concepts, Func<Concept, IReadOnlyCollection<string>> termsSelector, bool isPhrase)
    {
        var matches = new List<ConceptMatch>();

        foreach (var concept in concepts)
        {
            foreach (var term in termsSelector(concept))
            {
                var index = NormalizerService.GetTermIndex(value, term);

                if (index < 0)
                {
                    continue;
                }

                matches.Add(new ConceptMatch(concept, term, index, isPhrase));
            }
        }
        return matches;
    }

    private ConceptMatch SelectBestMatch(IEnumerable<ConceptMatch> matches)
    {
        return matches
            .OrderBy(match => match.Index)
            .ThenByDescending(match => NormalizerService.Normalize(match.Term).Length)
            .ThenByDescending(match => match.IsPhrase)
            .First();
    }
    
}