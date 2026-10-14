using System.Text.Json;
using PriceComparator.Application.Interfaces;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.Repositories;

public sealed class ConceptRepository : IConceptRepository, ISingletonDependency
{
    private readonly string _filePath;

    public ConceptRepository()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "Data", "product-concepts.json");
    }
    
    public async Task<IReadOnlyCollection<Concept>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(_filePath);
        var concepts = await JsonSerializer.DeserializeAsync<List<Concept>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        return concepts ?? [];
    }

    
}