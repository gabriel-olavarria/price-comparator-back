using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Models;

public sealed record ConceptMatch(Concept Concept, string Term, int Index, bool IsPhrase);