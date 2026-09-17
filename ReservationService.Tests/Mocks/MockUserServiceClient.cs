using ReservationService.Models.Dtos;
using ReservationService.Services.HttpClients;

namespace ReservationService.Tests.Mocks;

public class MockUserServiceClient : IUserServiceClient
{
    public Dictionary<Guid, UserValidationResult> Users { get; } = new();

    public Task<UserValidationResult?> ValidateUserAsync(Guid userId)
    {
        Users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }
}