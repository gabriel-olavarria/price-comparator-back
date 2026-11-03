namespace PriceComparator.Domain.Common;

public abstract class Entity<TId>
{
    public TId Id { get; set; } = default!;
}