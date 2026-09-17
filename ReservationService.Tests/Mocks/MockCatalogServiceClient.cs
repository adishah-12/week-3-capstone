using ReservationService.Models.Dtos;
using ReservationService.Services.HttpClients;

namespace ReservationService.Tests.Mocks;

public class MockCatalogServiceClient : ICatalogServiceClient
{
    public Dictionary<Guid, BookAvailabilityResult> Books { get; } = new();
    public List<(Guid BookId, int Delta)> AvailabilityUpdates { get; } = new();

    public Task<BookAvailabilityResult?> GetBookAsync(Guid bookId)
    {
        Books.TryGetValue(bookId, out var book);
        return Task.FromResult(book);
    }

    public Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta)
    {
        AvailabilityUpdates.Add((bookId, delta));

        if (Books.TryGetValue(bookId, out var book))
        {
            book.AvailableCopies += delta;
        }

        return Task.FromResult(true);
    }
}