using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using UserService.Models.Dtos;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserServiceContext _db;

    public UsersController(UserServiceContext db)
    {
        _db = db;
    }

    [HttpGet("{userId}/validate")]
    public async Task<IActionResult> Validate(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);

        if (user is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = $"User not found with ID: {userId}",
                timestamp = DateTime.UtcNow
            });
        }

        return Ok(new UserValidationResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString().ToUpper(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpper()
        });
    }
}