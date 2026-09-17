using Microsoft.EntityFrameworkCore;
using UserService.Models.Entities;
using UserService.Services;

namespace UserService.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(UserServiceContext context, PasswordHasher hasher)
    {
        if (await context.Users.AnyAsync()) return;

        var librarian = new User
        {
            UserId = Guid.NewGuid(),
            Email = "librarian@library.com",
            PasswordHash = hasher.Hash("Librarian123!"),
            FirstName = "Lib",
            LastName = "Rarian",
            PhoneNumber = "+1-555-0001",
            Role = Role.Librarian,
            MembershipStatus = MembershipStatus.Active,
            MemberSince = DateTime.UtcNow
        };

        await context.Users.AddAsync(librarian);
        await context.SaveChangesAsync();
    }
}