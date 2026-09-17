using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models.Dtos;
using ReservationService.Models.Entities;
using ReservationService.Services;
using ReservationService.Services.HttpClients;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly ReservationServiceContext _db;
    private readonly ICatalogServiceClient _catalogClient;

    public WaitlistController(ReservationServiceContext db, ICatalogServiceClient catalogClient)
    {
        _db = db;
        _catalogClient = catalogClient;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue("userId")!);

    [HttpPost]
    public async Task<IActionResult> Join(JoinWaitlistRequest request)
    {
        var userId = CurrentUserId;

        var book = await _catalogClient.GetBookAsync(request.BookId);
        if (book is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = $"Book not found with ID: {request.BookId}",
                timestamp = DateTime.UtcNow
            });
        }

        if (book.AvailableCopies > 0)
        {
            return BadRequest(new
            {
                error = "BOOK_AVAILABLE",
                message = "This book currently has available copies - reserve it directly instead of joining the waitlist",
                timestamp = DateTime.UtcNow
            });
        }

        var alreadyWaiting = await _db.WaitlistEntries.AnyAsync(w =>
            w.BookId == request.BookId && w.UserId == userId && w.Status == WaitlistStatus.Waiting);

        if (alreadyWaiting)
        {
            return BadRequest(new
            {
                error = "ALREADY_WAITLISTED",
                message = "You are already on the waitlist for this book",
                timestamp = DateTime.UtcNow
            });
        }

        var entry = new Waitlist
        {
            WaitlistId = Guid.NewGuid(),
            BookId = request.BookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = book.Title,
            BookAuthor = book.Author
        };

        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        var position = await _db.WaitlistEntries
            .Where(w => w.BookId == request.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt)
            .CountAsync();

        return StatusCode(201, new
        {
            waitlistId = entry.WaitlistId,
            bookId = entry.BookId,
            bookTitle = entry.BookTitle,
            status = entry.Status.ToString().ToUpper(),
            joinedAt = entry.JoinedAt,
            position
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyEntries()
    {
        var userId = CurrentUserId;

        var entries = await _db.WaitlistEntries
            .Where(w => w.UserId == userId && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .ToListAsync();

        var result = new List<WaitlistEntryDto>();

        foreach (var entry in entries)
        {
            int? position = null;
            if (entry.Status == WaitlistStatus.Waiting)
            {
                position = await _db.WaitlistEntries
                    .Where(w => w.BookId == entry.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt)
                    .CountAsync();
            }

            result.Add(new WaitlistEntryDto
            {
                WaitlistId = entry.WaitlistId,
                BookId = entry.BookId,
                BookTitle = entry.BookTitle,
                BookAuthor = entry.BookAuthor,
                Status = entry.Status.ToString().ToUpper(),
                JoinedAt = entry.JoinedAt,
                Position = position,
                NotifiedAt = entry.Status == WaitlistStatus.Notified ? entry.NotifiedAt : null,
                ClaimDeadline = entry.Status == WaitlistStatus.Notified ? entry.ClaimDeadline : null
            });
        }

        return Ok(new { entries = result });
    }

    [HttpDelete("{waitlistId}")]
    public async Task<IActionResult> Leave(Guid waitlistId)
    {
        var userId = CurrentUserId;

        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w =>
            w.WaitlistId == waitlistId && w.UserId == userId);

        if (entry is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = "Waitlist entry not found",
                timestamp = DateTime.UtcNow
            });
        }

        var wasNotified = entry.Status == WaitlistStatus.Notified;
        entry.Status = WaitlistStatus.Cancelled;
        await _db.SaveChangesAsync();

        if (wasNotified)
        {
            await CascadeService.ReleaseOrCascadeAsync(_db, _catalogClient, entry.BookId);
        }

        return Ok(new
        {
            waitlistId = entry.WaitlistId,
            status = entry.Status.ToString().ToUpper(),
            message = "You have been removed from the waitlist"
        });
    }
}