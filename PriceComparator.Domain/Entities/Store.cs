namespace PriceComparator.Domain.Entities;

public sealed class Store
{
    public string Code { get; }
    public string Name { get; }

    public Store(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "El código de la tienda es obligatorio.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "El nombre de la tienda es obligatorio.",
                nameof(name));
        }

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }
}

