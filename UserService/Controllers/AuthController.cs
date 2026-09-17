using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models.Dtos;
using UserService.Models.Entities;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserServiceContext _db;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtTokenService _tokenService;

    public AuthController(UserServiceContext db, PasswordHasher passwordHasher, JwtTokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Email already exists"
            });
        }

        if (!IsPasswordValid(request.Password))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Password must be at least 8 characters and include uppercase, lowercase, number, and special character"
            });
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Role = Role.Patron,
            MembershipStatus = MembershipStatus.Active,
            MemberSince = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            userId = user.UserId,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            role = user.Role.ToString().ToUpper(),
            membershipStatus = user.MembershipStatus.ToString().ToUpper(),
            createdAt = user.CreatedAt,
            message = "Registration successful"
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "AUTHENTICATION_FAILED",
                Message = "Invalid email or password"
            });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresIn = 86400,
            user = new
            {
                userId = user.UserId,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                role = user.Role.ToString().ToUpper()
            }
        });
    }

    private static bool IsPasswordValid(string password)
    {
        if (password.Length < 8) return false;
        if (!password.Any(char.IsUpper)) return false;
        if (!password.Any(char.IsLower)) return false;
        if (!password.Any(char.IsDigit)) return false;
        if (password.All(char.IsLetterOrDigit)) return false; // needs a special char
        return true;
    }
}