using Microsoft.EntityFrameworkCore;
using CatalogService.Models.Entities;

namespace CatalogService.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(CatalogServiceContext context)
    {
        if (await context.Books.AnyAsync()) return;

        var books = new List<Book>
        {
            new()
            {
                BookId = Guid.NewGuid(),
                Isbn = "978-0-13-468599-1",
                Title = "Clean Code",
                Author = "Robert C. Martin",
                Genre = "Technology",
                PublicationYear = 2008,
                Description = "A handbook of agile software craftsmanship",
                Publisher = "Prentice Hall",
                PageCount = 464,
                Language = "English",
                TotalCopies = 5,
                AvailableCopies = 2
            },
            new()
            {
                BookId = Guid.NewGuid(),
                Isbn = "978-0-13-475759-9",
                Title = "Refactoring",
                Author = "Martin Fowler",
                Genre = "Technology",
                PublicationYear = 2018,
                Description = "Improving the design of existing code",
                Publisher = "Addison-Wesley",
                PageCount = 448,
                Language = "English",
                TotalCopies = 3,
                AvailableCopies = 0
            },
            new()
            {
                BookId = Guid.NewGuid(),
                Isbn = "978-0-452-28423-4",
                Title = "1984",
                Author = "George Orwell",
                Genre = "Fiction",
                PublicationYear = 1949,
                Description = "A dystopian social science fiction novel",
                Publisher = "Secker & Warburg",
                PageCount = 328,
                Language = "English",
                TotalCopies = 4,
                AvailableCopies = 4
            }
        };

        await context.Books.AddRangeAsync(books);
        await context.SaveChangesAsync();
    }
}