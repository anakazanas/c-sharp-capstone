using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserServiceContext _context;
    private readonly IReservationServiceClient _reservationServiceClient;

    public UsersController(UserServiceContext context, IReservationServiceClient reservationServiceClient)
    {
        _context = context;
        _reservationServiceClient = reservationServiceClient;
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });
        }

        var user = await _context.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });
        }

        var stats = await _reservationServiceClient.GetStatisticsAsync(userId);

        return Ok(new
        {
            userId = user.UserId,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            phoneNumber = user.PhoneNumber,
            role = user.Role.ToString().ToUpperInvariant(),
            membershipStatus = user.MembershipStatus.ToString().ToUpperInvariant(),
            memberSince = user.MemberSince,
            activeReservations = stats?.ActiveReservations ?? 0,
            borrowingHistory = stats?.BorrowingHistory ?? 0
        });
    }

    [HttpGet("{userId:guid}/validate")]
    public async Task<IActionResult> Validate(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "User not found" });
        }

        if (user.MembershipStatus != MembershipStatus.Active)
        {
            return BadRequest(new ErrorResponse { Error = "USER_SUSPENDED", Message = "User membership is not active" });
        }

        var stats = await _reservationServiceClient.GetStatisticsAsync(userId);

        return Ok(new
        {
            userId = user.UserId,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            role = user.Role.ToString().ToUpperInvariant(),
            membershipStatus = user.MembershipStatus.ToString().ToUpperInvariant(),
            activeReservationsCount = stats?.ActiveReservations ?? 0
        });
    }
}
