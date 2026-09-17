using ReservationService.Models.Dtos;

namespace ReservationService.Services.HttpClients;

public interface ICatalogServiceClient
{
    Task<BookAvailabilityResult?> GetBookAsync(Guid bookId);
    Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta);
}