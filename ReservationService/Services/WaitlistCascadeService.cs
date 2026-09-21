using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public class WaitlistCascadeService : IWaitlistCascadeService
{
    private readonly ReservationServiceContext _context;
    private readonly ICatalogServiceClient _catalogServiceClient;
    private readonly ILogger<WaitlistCascadeService> _logger;

    public WaitlistCascadeService(
        ReservationServiceContext context,
        ICatalogServiceClient catalogServiceClient,
        ILogger<WaitlistCascadeService> logger)
    {
        _context = context;
        _catalogServiceClient = catalogServiceClient;
        _logger = logger;
    }

    public async Task ProcessCascadeAsync(Guid bookId)
    {
        while (true)
        {
            var nextEntry = await _context.WaitlistEntries
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (nextEntry is null)
            {
                await _catalogServiceClient.UpdateAvailabilityAsync(bookId, 1);
                _logger.LogInformation("No eligible waitlist entry for book {BookId}; released copy to general availability", bookId);
                return;
            }

            var activeCount = await _context.Reservations.CountAsync(r =>
                r.UserId == nextEntry.UserId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

            if (activeCount >= 5)
            {
                nextEntry.Status = WaitlistStatus.Expired;
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Waitlist entry {WaitlistId} for user {UserId} skipped (at 5-reservation limit); checking next in queue",
                    nextEntry.WaitlistId, nextEntry.UserId);
                continue;
            }

            var now = DateTime.UtcNow;
            var reservation = new Reservation
            {
                BookId = nextEntry.BookId,
                UserId = nextEntry.UserId,
                BookTitle = nextEntry.BookTitle,
                BookAuthor = nextEntry.BookAuthor,
                Status = ReservationStatus.Reserved,
                ReservedAt = now,
                ExpiresAt = now.AddDays(7)
            };
            _context.Reservations.Add(reservation);

            nextEntry.Status = WaitlistStatus.Notified;
            nextEntry.NotifiedAt = now;
            nextEntry.ClaimDeadline = now.AddHours(48);
            nextEntry.ResultingReservationId = reservation.ReservationId;

            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Waitlist entry {WaitlistId} notified for book {BookId}; reservation {ReservationId} created, claim deadline {Deadline}",
                nextEntry.WaitlistId, bookId, reservation.ReservationId, nextEntry.ClaimDeadline);
            return;
        }
    }

    public async Task ExpireNotifiedEntryAsync(WaitlistEntry entry, WaitlistStatus terminalStatus)
    {
        entry.Status = terminalStatus;

        if (entry.ResultingReservationId is not null)
        {
            var reservation = await _context.Reservations.FindAsync(entry.ResultingReservationId.Value);
            if (reservation is not null && reservation.Status == ReservationStatus.Reserved)
            {
                reservation.Status = ReservationStatus.Cancelled;
            }
        }

        await _context.SaveChangesAsync();
        await ProcessCascadeAsync(entry.BookId);
    }
}
