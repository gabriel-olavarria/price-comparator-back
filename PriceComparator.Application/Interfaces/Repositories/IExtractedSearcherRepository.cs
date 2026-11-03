using PriceComparator.Application.DTOs.Extractions;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface IExtractedSearcherRepository
{
    /// <summary>
    /// Busca una extracción realizada durante el día actual
    /// según la zona horaria de Chile.
    /// Retorna únicamente los metadatos.
    /// </summary>
    Task<ExtractedTodayDto?> FindTodayAsync(string store, string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda una extracción completa en Neon PostgreSQL.
    /// Si ya existe una extracción para el supermercado
    /// y la búsqueda, actualiza el registro.
    /// </summary>
    Task SaveAsync(RawExtractionDto data, CancellationToken cancellationToken = default);
}