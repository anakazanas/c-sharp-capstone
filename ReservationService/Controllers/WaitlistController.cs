using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly ReservationServiceContext _context;
    private readonly ICatalogServiceClient _catalogServiceClient;
    private readonly IWaitlistCascadeService _cascadeService;

    public WaitlistController(
        ReservationServiceContext context,
        ICatalogServiceClient catalogServiceClient,
        IWaitlistCascadeService cascadeService)
    {
        _context = context;
        _catalogServiceClient = catalogServiceClient;
        _cascadeService = cascadeService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Join(JoinWaitlistRequest request)
    {
        var userId = CurrentUserId;

        var book = await _catalogServiceClient.GetBookAsync(request.BookId);
        if (book is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Book not found" });
        }

        if (book.AvailableCopies > 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "BOOK_AVAILABLE",
                Message = "This book currently has available copies - reserve it directly instead of joining the waitlist"
            });
        }

        var alreadyWaiting = await _context.WaitlistEntries.AnyAsync(w =>
            w.BookId == request.BookId && w.UserId == userId && w.Status == WaitlistStatus.Waiting);

        if (alreadyWaiting)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "ALREADY_WAITLISTED",
                Message = "You are already on the waitlist for this book"
            });
        }

        var entry = new WaitlistEntry
        {
            BookId = request.BookId,
            UserId = userId,
            BookTitle = book.Title,
            BookAuthor = book.Author,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow
        };

        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync();

        var position = await _context.WaitlistEntries.CountAsync(w =>
            w.BookId == request.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt);

        return StatusCode(201, new JoinWaitlistResponse
        {
            WaitlistId = entry.WaitlistId,
            BookId = entry.BookId,
            BookTitle = entry.BookTitle,
            Status = entry.Status.ToString().ToUpperInvariant(),
            JoinedAt = entry.JoinedAt,
            Position = position
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId;

        var entries = await _context.WaitlistEntries
            .Where(w => w.UserId == userId && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .ToListAsync();

        var dtos = new List<WaitlistEntryDto>();
        foreach (var entry in entries)
        {
            int? position = null;
            if (entry.Status == WaitlistStatus.Waiting)
            {
                position = await _context.WaitlistEntries.CountAsync(w =>
                    w.BookId == entry.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt);
            }

            dtos.Add(new WaitlistEntryDto
            {
                WaitlistId = entry.WaitlistId,
                BookId = entry.BookId,
                BookTitle = entry.BookTitle,
                BookAuthor = entry.BookAuthor,
                Status = entry.Status.ToString().ToUpperInvariant(),
                JoinedAt = entry.JoinedAt,
                Position = position,
                NotifiedAt = entry.NotifiedAt,
                ClaimDeadline = entry.ClaimDeadline
            });
        }

        return Ok(new { entries = dtos });
    }

    [HttpDelete("{waitlistId:guid}")]
    public async Task<IActionResult> Leave(Guid waitlistId)
    {
        var userId = CurrentUserId;

        var entry = await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.WaitlistId == waitlistId && w.UserId == userId);
        if (entry is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Waitlist entry not found" });
        }

        if (entry.Status != WaitlistStatus.Waiting && entry.Status != WaitlistStatus.Notified)
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_STATUS", Message = "This waitlist entry can no longer be cancelled" });
        }

        if (entry.Status == WaitlistStatus.Notified)
        {
            await _cascadeService.ExpireNotifiedEntryAsync(entry, WaitlistStatus.Cancelled);
        }
        else
        {
            entry.Status = WaitlistStatus.Cancelled;
            await _context.SaveChangesAsync();
        }

        return Ok(new { waitlistId = entry.WaitlistId, status = "CANCELLED", message = "You have been removed from the waitlist" });
    }
}
