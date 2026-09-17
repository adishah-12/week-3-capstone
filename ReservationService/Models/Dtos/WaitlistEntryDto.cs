namespace ReservationService.Models.Dtos;

public class WaitlistEntryDto
{
    public Guid WaitlistId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int? Position { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public DateTime? ClaimDeadline { get; set; }
}