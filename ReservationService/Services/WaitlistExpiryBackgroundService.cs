using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public class WaitlistExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WaitlistExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public WaitlistExpiryBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<WaitlistExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var minutes = configuration.GetValue<int?>("Waitlist:ExpiryCheckIntervalMinutes") ?? 60;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Waitlist expiry background job started, checking every {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredEntriesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing waitlist expiry");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredEntriesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
        var cascadeService = scope.ServiceProvider.GetRequiredService<IWaitlistCascadeService>();

        var now = DateTime.UtcNow;
        var expiredEntries = await context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Notified && w.ClaimDeadline.HasValue && w.ClaimDeadline.Value < now)
            .ToListAsync();

        if (expiredEntries.Count == 0)
        {
            _logger.LogInformation("Waitlist expiry check: no expired claims found");
            return;
        }

        _logger.LogInformation("Waitlist expiry check: found {Count} expired claim(s)", expiredEntries.Count);

        foreach (var entry in expiredEntries)
        {
            _logger.LogInformation(
                "Expiring waitlist entry {WaitlistId} for book {BookId} (claim deadline {Deadline} passed)",
                entry.WaitlistId, entry.BookId, entry.ClaimDeadline);

            await cascadeService.ExpireNotifiedEntryAsync(entry, WaitlistStatus.Expired);
        }
    }
}
