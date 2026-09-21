using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly ReservationServiceContext _context;
    private readonly ICatalogServiceClient _catalogServiceClient;
    private readonly IWaitlistCascadeService _cascadeService;

    public ReservationsController(ReservationServiceContext context, ICatalogServiceClient catalogServiceClient, IWaitlistCascadeService cascadeService)
    {
        _context = context;
        _catalogServiceClient = catalogServiceClient;
        _cascadeService = cascadeService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string StatusToContractString(ReservationStatus status) => status switch
    {
        ReservationStatus.Reserved => "RESERVED",
        ReservationStatus.CheckedOut => "CHECKED_OUT",
        ReservationStatus.Returned => "RETURNED",
        ReservationStatus.Cancelled => "CANCELLED",
        _ => status.ToString().ToUpperInvariant()
    };

    [HttpPost]
    public async Task<IActionResult> Create(CreateReservationRequest request)
    {
        var userId = CurrentUserId;

        var activeCount = await _context.Reservations.CountAsync(r =>
            r.UserId == userId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

        if (activeCount >= 5)
        {
            return BadRequest(new
            {
                error = "RESERVATION_LIMIT_EXCEEDED",
                message = "You have reached the maximum of 5 active reservations",
                currentReservations = activeCount
            });
        }

        var book = await _catalogServiceClient.GetBookAsync(request.BookId);
        if (book is null || book.AvailableCopies <= 0)
        {
            return BadRequest(new
            {
                error = "BOOK_UNAVAILABLE",
                message = "No copies available for reservation",
                availableCopies = book?.AvailableCopies ?? 0
            });
        }

        var decremented = await _catalogServiceClient.UpdateAvailabilityAsync(request.BookId, -1);
        if (!decremented)
        {
            return StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = "Failed to update book availability" });
        }

        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            BookId = request.BookId,
            UserId = userId,
            BookTitle = book.Title,
            BookAuthor = book.Author,
            Status = ReservationStatus.Reserved,
            ReservedAt = now,
            ExpiresAt = now.AddDays(7)
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return StatusCode(201, new ReservationResponse
        {
            ReservationId = reservation.ReservationId,
            BookId = reservation.BookId,
            UserId = reservation.UserId,
            BookTitle = reservation.BookTitle,
            Status = StatusToContractString(reservation.Status),
            ReservedAt = reservation.ReservedAt,
            ExpiresAt = reservation.ExpiresAt!.Value,
            Message = "Book reserved successfully. Please pick up within 7 days."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var reservations = await _context.Reservations
            .Where(r => r.UserId == userId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut))
            .ToListAsync();

        var dtos = reservations.Select(r => new ActiveReservationDto
        {
            ReservationId = r.ReservationId,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            Status = StatusToContractString(r.Status),
            ReservedAt = r.Status == ReservationStatus.Reserved ? r.ReservedAt : null,
            ExpiresAt = r.Status == ReservationStatus.Reserved ? r.ExpiresAt : null,
            DaysUntilExpiry = r.Status == ReservationStatus.Reserved && r.ExpiresAt.HasValue
                ? (int?)Math.Ceiling((r.ExpiresAt.Value - now).TotalDays)
                : null,
            CheckedOutAt = r.Status == ReservationStatus.CheckedOut ? r.CheckedOutAt : null,
            DueDate = r.Status == ReservationStatus.CheckedOut ? r.DueDate : null,
            DaysUntilDue = r.Status == ReservationStatus.CheckedOut && r.DueDate.HasValue
                ? (int?)Math.Ceiling((r.DueDate.Value - now).TotalDays)
                : null
        }).ToList();

        return Ok(new ActiveReservationsResponse
        {
            Reservations = dtos,
            TotalActive = dtos.Count
        });
    }

    [Authorize(Roles = "Librarian")]
    [HttpPost("{reservationId:guid}/checkout")]
    public async Task<IActionResult> Checkout(Guid reservationId, CheckoutRequest request)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Reservation not found" });
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only checkout reservations with RESERVED status",
                currentStatus = StatusToContractString(reservation.Status)
            });
        }

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = now;
        reservation.DueDate = now.AddDays(14);
        reservation.Notes = request.Notes;

        await _context.SaveChangesAsync();

        return Ok(new CheckoutResponse
        {
            ReservationId = reservation.ReservationId,
            Status = StatusToContractString(reservation.Status),
            CheckedOutAt = reservation.CheckedOutAt.Value,
            DueDate = reservation.DueDate.Value,
            Message = $"Book checked out successfully. Due date: {reservation.DueDate.Value:MMMM d, yyyy}"
        });
    }

    [Authorize(Roles = "Librarian")]
    [HttpPost("{reservationId:guid}/return")]
    public async Task<IActionResult> Return(Guid reservationId, ReturnRequest request)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Reservation not found" });
        }

        if (reservation.Status != ReservationStatus.CheckedOut)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only return books with CHECKED_OUT status",
                currentStatus = StatusToContractString(reservation.Status)
            });
        }

        if (!Enum.TryParse<BookCondition>(request.Condition, ignoreCase: true, out var condition))
        {
            return BadRequest(new ErrorResponse { Error = "VALIDATION_ERROR", Message = "Condition must be one of: Good, Fair, Poor, Damaged" });
        }

        var now = DateTime.UtcNow;
        var lateDays = reservation.DueDate.HasValue && now > reservation.DueDate.Value
            ? (int)Math.Ceiling((now - reservation.DueDate.Value).TotalDays)
            : 0;
        var lateFee = lateDays * 1.00m;

        reservation.Status = ReservationStatus.Returned;
        reservation.ReturnedAt = now;
        reservation.Condition = condition;
        reservation.Notes = request.Notes;
        reservation.LateDays = lateDays;
        reservation.LateFee = lateFee;

        // Hand the copy to the next eligible waitlist entry, or release it to general availability if none.
        await _cascadeService.ProcessCascadeAsync(reservation.BookId);

        await _context.SaveChangesAsync();

        var message = lateFee > 0
            ? $"Book returned. Late fee of ${lateFee:F2} applied to account."
            : "Book returned successfully";

        return Ok(new ReturnResponse
        {
            ReservationId = reservation.ReservationId,
            ReturnedAt = reservation.ReturnedAt.Value,
            DueDate = reservation.DueDate,
            LateDays = lateDays,
            LateFee = lateFee,
            Message = message
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        var userId = CurrentUserId;

        var query = _context.Reservations
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReturnedAt ?? r.ReservedAt);

        var totalElements = await query.CountAsync();
        var totalPages = size > 0 ? (int)Math.Ceiling(totalElements / (double)size) : 0;

        var pageItems = await query.Skip(page * size).Take(size).ToListAsync();

        var content = pageItems.Select(r => new HistoryRecordDto
        {
            ReservationId = r.ReservationId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            ReservedAt = r.ReservedAt,
            CheckedOutAt = r.CheckedOutAt,
            ReturnedAt = r.ReturnedAt,
            DueDate = r.DueDate,
            Status = StatusToContractString(r.Status),
            WasLate = r.ReturnedAt.HasValue && r.DueDate.HasValue && r.ReturnedAt.Value > r.DueDate.Value
        }).ToList();

        return Ok(new PagedHistoryResult
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }
}
