using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Data;
using CatalogService.Models.Dtos;
using CatalogService.Models.Entities;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly CatalogServiceContext _db;

    public CatalogController(CatalogServiceContext db)
    {
        _db = db;
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string sortBy = "title",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] string? query = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? isbn = null,
        [FromQuery] bool availableOnly = false)
    {
        var books = _db.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            books = books.Where(b => b.Title.Contains(query) || b.Author.Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            books = books.Where(b => b.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            books = books.Where(b => b.Isbn == isbn);
        }

        if (availableOnly)
        {
            books = books.Where(b => b.AvailableCopies > 0);
        }

        bool descending = sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        books = sortBy.ToLower() switch
        {
            "author" => descending ? books.OrderByDescending(b => b.Author) : books.OrderBy(b => b.Author),
            "publicationyear" => descending ? books.OrderByDescending(b => b.PublicationYear) : books.OrderBy(b => b.PublicationYear),
            _ => descending ? books.OrderByDescending(b => b.Title) : books.OrderBy(b => b.Title)
        };

        var totalElements = await books.CountAsync();
        var totalPages = (int)Math.Ceiling(totalElements / (double)size);

        var pageItems = await books
            .Skip(page * size)
            .Take(size)
            .Select(b => ToListItem(b))
            .ToListAsync();

        return Ok(new PagedResponse<BookListItem>
        {
            Content = pageItems,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("books/{bookId}")]
    public async Task<IActionResult> GetBookById(Guid bookId)
    {
        var book = await _db.Books.FindAsync(bookId);

        if (book is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = $"Book not found with ID: {bookId}",
                timestamp = DateTime.UtcNow
            });
        }

        return Ok(new BookDetail
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = CalculateStatus(book),
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }

    private static BookListItem ToListItem(Book b) => new()
    {
        BookId = b.BookId,
        Isbn = b.Isbn,
        Title = b.Title,
        Author = b.Author,
        Genre = b.Genre,
        PublicationYear = b.PublicationYear,
        Description = b.Description,
        TotalCopies = b.TotalCopies,
        AvailableCopies = b.AvailableCopies,
        Status = CalculateStatus(b)
    };

    private static string CalculateStatus(Book b) => b.AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT";

    [HttpPut("books/{bookId}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid bookId, AvailabilityUpdateRequest request)
    {
        var book = await _db.Books.FindAsync(bookId);

        if (book is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = $"Book not found with ID: {bookId}",
                timestamp = DateTime.UtcNow
            });
        }

        var newAvailable = book.AvailableCopies + request.Delta;

        if (newAvailable < 0 || newAvailable > book.TotalCopies)
        {
            return BadRequest(new
            {
                error = "INVALID_AVAILABILITY",
                message = "Resulting availableCopies would be out of range",
                timestamp = DateTime.UtcNow
            });
        }

        book.AvailableCopies = newAvailable;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            bookId = book.BookId,
            availableCopies = book.AvailableCopies,
            totalCopies = book.TotalCopies
        });
    }
}