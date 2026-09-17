namespace ReservationService.Models.Dtos;

public class BookAvailabilityResult
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int AvailableCopies { get; set; }
    public int TotalCopies { get; set; }
}