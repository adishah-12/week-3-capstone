using ReservationService.Models.Dtos;

namespace ReservationService.Services.HttpClients;

public interface IUserServiceClient
{
    Task<UserValidationResult?> ValidateUserAsync(Guid userId);
}