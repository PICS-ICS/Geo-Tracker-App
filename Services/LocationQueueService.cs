using System.Diagnostics;
using GeoTrackerApp3.Models;
using SQLite;

namespace GeoTrackerApp3.Services;

public class PendingLocationRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int MemberID { get; set; }
    public int CompanyID { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public DateTime PingTime { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceOS { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
}

public static class LocationQueueService
{
    private static readonly string DbPath =
        Path.Combine(FileSystem.AppDataDirectory, "pending_locations.db");

    private static readonly SQLiteAsyncConnection _db = new(DbPath);
    private static bool _initialized;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static async Task InitAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<PendingLocationRecord>();
        _initialized = true;
    }

    /// <summary>
    /// Enqueues a failed location ping for later sync.
    /// </summary>
    public static async Task EnqueueAsync(LocationData data)
    {
        await _lock.WaitAsync();
        try
        {
            await InitAsync();
            var record = new PendingLocationRecord
            {
                MemberID = data.memberID,
                CompanyID = data.companyID,
                Lat = data.lat,
                Lon = data.lon,
                PingTime = data.pingTime,
                IpAddress = data.ipAddress,
                DeviceOS = data.deviceOS,
                DeviceModel = data.deviceModel
            };
            await _db.InsertAsync(record);
            Debug.WriteLine($"[LocationQueue] Enqueued ping from {data.pingTime}. Total: {await _db.Table<PendingLocationRecord>().CountAsync()}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Attempts to send all queued location pings as a batch. Removes successful ones.
    /// </summary>
    public static async Task SyncAsync(string token)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return;

        await _lock.WaitAsync();
        try
        {
            await InitAsync();
            var pending = await _db.Table<PendingLocationRecord>().Take(100).ToListAsync();
            if (pending.Count == 0) return;

            Debug.WriteLine($"[LocationQueue] Syncing {pending.Count} pending pings...");

            var batch = pending.Select(r => new LocationData
            {
                memberID = r.MemberID,
                companyID = r.CompanyID,
                lat = r.Lat,
                lon = r.Lon,
                pingTime = r.PingTime,
                ipAddress = r.IpAddress,
                deviceOS = r.DeviceOS,
                deviceModel = r.DeviceModel
            }).ToList();

            var result = await ApiService.SendLocationBatchAsync(batch, token);

            if (result.IsSuccess)
            {
                // Delete all synced records
                foreach (var record in pending)
                    await _db.DeleteAsync(record);

                Debug.WriteLine($"[LocationQueue] Batch synced {pending.Count} pings");

                // Check if there are more to sync
                var remaining = await _db.Table<PendingLocationRecord>().CountAsync();
                if (remaining > 0)
                {
                    // Release lock before recursive call
                    _lock.Release();
                    await SyncAsync(token);
                    return; // skip the finally Release
                }
            }
            else
            {
                Debug.WriteLine($"[LocationQueue] Batch sync failed: {result.ErrorMessage}");
            }
        }
        finally
        {
            if (_lock.CurrentCount == 0)
                _lock.Release();
        }
    }

    /// <summary>
    /// Returns the number of pending (unsynced) location pings.
    /// </summary>
    public static async Task<int> GetPendingCountAsync()
    {
        await InitAsync();
        return await _db.Table<PendingLocationRecord>().CountAsync();
    }
}
