using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserServiceContext _context;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(
        UserServiceContext context,
        IValidator<RegisterRequest> registerValidator,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _context = context;
        _registerValidator = registerValidator;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        var validation = await _registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = validation.Errors.First().ErrorMessage
            });
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Email already exists"
            });
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Role = Role.Patron,
            MembershipStatus = MembershipStatus.Active,
            MemberSince = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return StatusCode(201, new RegisterResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString().ToUpperInvariant(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpperInvariant(),
            CreatedAt = user.CreatedAt,
            Message = "Registration successful"
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "AUTHENTICATION_FAILED",
                Message = "Invalid email or password"
            });
        }

        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 86400,
            User = new LoginUserSummary
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString().ToUpperInvariant()
            }
        });
    }
}
