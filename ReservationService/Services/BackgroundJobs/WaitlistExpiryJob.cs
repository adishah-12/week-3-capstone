using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models.Entities;
using ReservationService.Services.HttpClients;

namespace ReservationService.Services.BackgroundJobs;

public class WaitlistExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistExpiryJob> _logger;
    private readonly TimeSpan _interval;

    public WaitlistExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistExpiryJob> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutes = config.GetValue<int?>("WaitlistExpiryJob:IntervalMinutes") ?? 60;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessExpiredEntriesAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredEntriesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
        var catalogClient = scope.ServiceProvider.GetRequiredService<CatalogServiceClient>();

        var now = DateTime.UtcNow;

        var expired = await db.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Notified && w.ClaimDeadline < now)
            .ToListAsync(stoppingToken);

        if (expired.Count == 0)
        {
            _logger.LogInformation("Waitlist expiry job ran: no expired entries found.");
            return;
        }

        _logger.LogInformation("Waitlist expiry job ran: found {Count} expired entries.", expired.Count);

        var affectedBookIds = expired.Select(w => w.BookId).Distinct().ToList();

        foreach (var entry in expired)
        {
            entry.Status = WaitlistStatus.Expired;
        }
        await db.SaveChangesAsync(stoppingToken);

        foreach (var bookId in affectedBookIds)
        {
            _logger.LogInformation("Cascading expired claim for book {BookId}.", bookId);
            await CascadeService.ReleaseOrCascadeAsync(db, catalogClient, bookId);
        }
    }
}