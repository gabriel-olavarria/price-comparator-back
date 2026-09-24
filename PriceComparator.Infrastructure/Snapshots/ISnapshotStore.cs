namespace PriceComparator.Infrastructure.Snapshots;

public interface ISnapshotStore
{
    Task SaveAsync(
        string storeCode,
        string query,
        string html,
        CancellationToken cancellationToken = default);
}