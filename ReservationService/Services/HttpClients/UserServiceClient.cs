using System.Net.Http.Json;
using ReservationService.Models.Dtos;

namespace ReservationService.Services.HttpClients;

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _http;

    public UserServiceClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<UserValidationResult?> ValidateUserAsync(Guid userId)
    {
        var response = await _http.GetAsync($"/api/users/{userId}/validate");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserValidationResult>();
    }
}