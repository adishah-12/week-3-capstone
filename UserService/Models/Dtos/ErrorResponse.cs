namespace UserService.Models.Dtos;

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}