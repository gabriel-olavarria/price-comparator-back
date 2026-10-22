using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Interfaces.Services;

public interface IConceptService
{
    Concept? DetectFromText(string query, IReadOnlyCollection<Concept> concepts);
}