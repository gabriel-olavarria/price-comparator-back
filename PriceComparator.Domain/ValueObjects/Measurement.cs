using PriceComparator.Domain.Enums;

namespace PriceComparator.Domain.ValueObjects;

public record Measurement
{
    /// <summary>
    /// Tipo de medida del producto.
    /// Ejemplo: "Bebida 3L" → Volume
    /// </summary>
    public UnitType Type { get; init; }

    /// <summary>
    /// Cantidad de unidades que contiene el producto.
    /// Ejemplo: "Bebida 3L" → 1
    /// Ejemplo: "Pack 6 un de 220 ml" → 6
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// Valor numérico de la medida, sin la unidad.
    /// Se normaliza el separador decimal a punto.
    /// Ejemplo: "Bebida 3L" → 3
    /// Ejemplo: "Bebida 2,5 L" → 2.5
    /// Ejemplo: "Pack 6 un de 220 ml" → 220
    /// </summary>
    public decimal ValueUnit { get; init; }

    /// <summary>
    /// Unidad de medida del producto.
    /// Ejemplo: "Bebida 3L" → L
    /// Ejemplo: "Pack 6 un de 220 ml" → Ml
    /// </summary>
    public Unit Unit { get; init; }

    /// <summary>
    /// Representación legible de la medida con su unidad abreviada.
    /// Ejemplo: "Bebida 3L" → "3 L"
    /// Ejemplo: "Bebida 2,5 L" → "2.5 L"
    /// Ejemplo: "Pack 6 un de 220 ml" → "220 Ml"
    /// </summary>
    public string DisplayUnit { get; init; } = string.Empty;

    /// <summary>
    /// Representación normalizada para generar el CanonicalKey.
    /// Sin espacios y con separador decimal normalizado.
    /// Ejemplo: "Bebida 3L" → "3L"
    /// Ejemplo: "Bebida 2,5 L" → "2.5L"
    /// Ejemplo: "Pack 6 un de 220 ml" → "220ml"
    /// </summary>
    public string DisplayCanKey { get; init; } = string.Empty;

    /// <summary>
    /// Representación completa de la medida con su descripción.
    /// Ejemplo: "Bebida 3L" → "3 Litros"
    /// Ejemplo: "Bebida 2,5 L" → "2.5 Litros"
    /// Ejemplo: "Pack 6 un de 220 ml" → "220 Mililitros"
    /// </summary>
    public string TotalDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Fragmento exacto encontrado en el nombre original del producto.
    /// Se utiliza para reemplazar o eliminar ese fragmento.
    /// Conserva espacios, comas y formato original.
    /// Ejemplo: "Bebida 3L" → "3L"
    /// Ejemplo: "Bebida 2,5 L" → "2,5 L"
    /// Ejemplo: "Pack 6 un de 220 ml" → "6 un de 220 ml"
    /// </summary>
    public string DisplayReplace { get; init; } = string.Empty;
}