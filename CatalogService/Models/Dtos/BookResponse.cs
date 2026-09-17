namespace CatalogService.Models.Dtos;

public class BookListItem
{
    public Guid BookId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int? PublicationYear { get; set; }
    public string? Description { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BookDetail : BookListItem
{
    public string? Publisher { get; set; }
    public int? PageCount { get; set; }
    public string? Language { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}