
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Application.Models;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.Services;

namespace PriceComparator.Application.Services;

public class ConceptService : IConceptService, ISingletonDependency
{
    public Concept? DetectFromText(
        string value,
        IReadOnlyCollection<Concept> concepts)
    {
        if (string.IsNullOrWhiteSpace(value) || concepts.Count == 0)
        {
            return null;
        }

        var matches = FindMatches(value, concepts);

        return matches.Count > 0
            ? SelectBestMatch(matches).Concept
            : null;
    }

    private static List<ConceptMatch> FindMatches(
        string value,
        IReadOnlyCollection<Concept> concepts)
    {
        var matches = new List<ConceptMatch>();

        foreach (var concept in concepts)
        {
            var terms = concept.Aliases
                .Select(alias => alias.Name)
                .Append(concept.Name)
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var term in terms)
            {
                var index = NormalizerService.GetTermIndex(value, term);

                if (index < 0)
                {
                    continue;
                }

                matches.Add(
                    new ConceptMatch(
                        concept,
                        term,
                        index,
                        false));
            }
        }

        return matches;
    }

    private static ConceptMatch SelectBestMatch(
        IEnumerable<ConceptMatch> matches)
    {
        return matches
            .OrderBy(match => match.Index)
            .ThenByDescending(match =>
                NormalizerService.Normalize(match.Term).Length)
            .ThenBy(match => match.Concept.Id)
            .First();
    }
}
