using ReservationService.Models;

namespace ReservationService.Services;

public interface IWaitlistCascadeService
{
    // A copy just became free for this book — hand it to the next eligible
    // waiting entry, or release it to general availability if none exists.
    Task ProcessCascadeAsync(Guid bookId);

    // A Notified entry is being terminated (cancelled by the patron, or expired
    // by the background job) — cancel its held reservation and cascade the copy.
    Task ExpireNotifiedEntryAsync(WaitlistEntry entry, WaitlistStatus terminalStatus);
}
