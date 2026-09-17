namespace ReservationService.Models.Dtos;

public class ReturnRequest
{
    public string Condition { get; set; } = string.Empty;
    public string? Notes { get; set; }
}