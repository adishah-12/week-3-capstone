using System.Net.Http.Json;
using ReservationService.Models.Dtos;

namespace ReservationService.Services.HttpClients;

public class CatalogServiceClient
{
    private readonly HttpClient _http;

    public CatalogServiceClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<BookAvailabilityResult?> GetBookAsync(Guid bookId)
    {
        var response = await _http.GetAsync($"/api/catalog/books/{bookId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BookAvailabilityResult>();
    }

    public async Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta)
    {
        var response = await _http.PutAsJsonAsync(
            $"/api/catalog/books/{bookId}/availability",
            new { delta });
        return response.IsSuccessStatusCode;
    }
}