using DonitGames.Core.Rooms;

namespace DonitGames.Web.Rooms;

/// <summary>Sweeps every registered game's rooms for inactivity, so an abandoned room (everyone
/// closed the tab, nobody came back) doesn't sit in memory for the life of the process.</summary>
public sealed class RoomJanitor(IEnumerable<IRoomRegistry> registries, ILogger<RoomJanitor> logger) : BackgroundService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(6);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var registry in registries)
            {
                var removed = registry.RemoveIdleRooms(IdleTimeout, now);
                if (removed > 0)
                {
                    logger.LogInformation("Removed {Count} idle room(s) from {Registry}", removed, registry.GetType().Name);
                }
            }
        }
    }
}
