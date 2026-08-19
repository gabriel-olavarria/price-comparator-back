namespace PriceComparator.Infrastructure.Snapshots;

public interface ISnapshotStore
{
    Task<string?> GetAsync(
        string storeCode,
        string query,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string storeCode,
        string query,
        string html,
        CancellationToken cancellationToken = default);
}