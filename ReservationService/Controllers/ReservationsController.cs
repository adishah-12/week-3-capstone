using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models.Dtos;
using ReservationService.Models.Entities;
using ReservationService.Services.HttpClients;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private const int MaxActiveReservations = 5;
    private const int ExpiryDays = 7;
    private const int CheckoutDays = 14;

    private readonly ReservationServiceContext _db;
    private readonly IUserServiceClient _userClient;
    private readonly ICatalogServiceClient _catalogClient;
    private const decimal LateFeePerDay = 1.00m;
    public ReservationsController(
        ReservationServiceContext db,
        IUserServiceClient userClient,
        ICatalogServiceClient catalogClient)
    {
        _db = db;
        _userClient = userClient;
        _catalogClient = catalogClient;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue("userId")!);

    private static string FormatStatus(ReservationStatus status) => status switch
    {
        ReservationStatus.CheckedOut => "CHECKED_OUT",
        _ => status.ToString().ToUpper()
    };

    [HttpPost]
    public async Task<IActionResult> Create(CreateReservationRequest request)
    {
        var userId = CurrentUserId;

        var activeCount = await _db.Reservations.CountAsync(r =>
            r.UserId == userId &&
            (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

        if (activeCount >= MaxActiveReservations)
        {
            return BadRequest(new
            {
                error = "RESERVATION_LIMIT_EXCEEDED",
                message = "You have reached the maximum of 5 active reservations",
                currentReservations = activeCount
            });
        }

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

        if (book.AvailableCopies <= 0)
        {
            return BadRequest(new
            {
                error = "BOOK_UNAVAILABLE",
                message = "No copies available for reservation",
                availableCopies = book.AvailableCopies
            });
        }

        var reservedAt = DateTime.UtcNow;
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookId = request.BookId,
            UserId = userId,
            Status = ReservationStatus.Reserved,
            ReservedAt = reservedAt,
            ExpiresAt = reservedAt.AddDays(ExpiryDays),
            BookTitle = book.Title,
            BookAuthor = book.Author
        };

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        await _catalogClient.UpdateAvailabilityAsync(request.BookId, -1);

        return StatusCode(201, new
        {
            reservationId = reservation.ReservationId,
            bookId = reservation.BookId,
            userId = reservation.UserId,
            bookTitle = reservation.BookTitle,
            status = FormatStatus(reservation.Status),
            reservedAt = reservation.ReservedAt,
            expiresAt = reservation.ExpiresAt,
            message = "Book reserved successfully. Please pick up within 7 days."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var reservations = await _db.Reservations
            .Where(r => r.UserId == userId &&
                (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut))
            .ToListAsync();

        var result = reservations.Select(r => new ActiveReservationDto
        {
            ReservationId = r.ReservationId,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            Status = FormatStatus(r.Status),
            ReservedAt = r.Status == ReservationStatus.Reserved ? r.ReservedAt : null,
            ExpiresAt = r.Status == ReservationStatus.Reserved ? r.ExpiresAt : null,
            DaysUntilExpiry = r.Status == ReservationStatus.Reserved && r.ExpiresAt.HasValue
                ? (int)Math.Ceiling((r.ExpiresAt.Value - now).TotalDays)
                : null,
            CheckedOutAt = r.Status == ReservationStatus.CheckedOut ? r.CheckedOutAt : null,
            DueDate = r.Status == ReservationStatus.CheckedOut ? r.DueDate : null,
            DaysUntilDue = r.Status == ReservationStatus.CheckedOut && r.DueDate.HasValue
                ? (int)Math.Ceiling((r.DueDate.Value - now).TotalDays)
                : null
        }).ToList();

        return Ok(new
        {
            reservations = result,
            totalActive = result.Count
        });
    }

    [HttpPost("{reservationId}/checkout")]
    [Authorize(Roles = "Librarian")]
    public async Task<IActionResult> Checkout(Guid reservationId, CheckoutRequest request)
    {
        var reservation = await _db.Reservations.FindAsync(reservationId);

        if (reservation is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = $"Reservation not found with ID: {reservationId}",
                timestamp = DateTime.UtcNow
            });
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only checkout reservations with RESERVED status",
                currentStatus = FormatStatus(reservation.Status)
            });
        }

        var checkedOutAt = DateTime.UtcNow;
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = checkedOutAt;
        reservation.DueDate = checkedOutAt.AddDays(CheckoutDays);
        reservation.Notes = request.Notes;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            reservationId = reservation.ReservationId,
            status = FormatStatus(reservation.Status),
            checkedOutAt = reservation.CheckedOutAt,
            dueDate = reservation.DueDate,
            message = $"Book checked out successfully. Due date: {reservation.DueDate:MMMM d, yyyy}"
        });
    }

    [HttpPost("{reservationId}/return")]
    [Authorize(Roles = "Librarian")]
    public async Task<IActionResult> Return(Guid reservationId, ReturnRequest request)
    {
        var reservation = await _db.Reservations.FindAsync(reservationId);

        if (reservation is null)
        {
            return NotFound(new
            {
                error = "NOT_FOUND",
                message = $"Reservation not found with ID: {reservationId}",
                timestamp = DateTime.UtcNow
            });
        }

        if (reservation.Status != ReservationStatus.CheckedOut)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only return books with CHECKED_OUT status",
                currentStatus = FormatStatus(reservation.Status)
            });
        }

        if (!Enum.TryParse<BookCondition>(request.Condition, true, out var condition))
        {
            return BadRequest(new
            {
                error = "VALIDATION_ERROR",
                message = "Condition must be one of: Good, Fair, Poor, Damaged"
            });
        }

        var returnedAt = DateTime.UtcNow;
        var lateDays = 0;
        if (reservation.DueDate.HasValue && returnedAt > reservation.DueDate.Value)
        {
            lateDays = (int)Math.Ceiling((returnedAt - reservation.DueDate.Value).TotalDays);
        }
        var lateFee = lateDays * LateFeePerDay;

        reservation.Status = ReservationStatus.Returned;
        reservation.ReturnedAt = returnedAt;
        reservation.Condition = condition;
        reservation.Notes = request.Notes;
        reservation.LateDays = lateDays;
        reservation.LateFee = lateFee;

        await _db.SaveChangesAsync();

        await CascadeService.ReleaseOrCascadeAsync(_db, _catalogClient, reservation.BookId);

        return Ok(new
        {
            reservationId = reservation.ReservationId,
            returnedAt = reservation.ReturnedAt,
            dueDate = reservation.DueDate,
            lateDays = reservation.LateDays,
            lateFee = reservation.LateFee,
            message = lateDays > 0
                ? $"Book returned. Late fee of ${lateFee:F2} applied to account."
                : "Book returned successfully"
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        var userId = CurrentUserId;

        var query = _db.Reservations
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReturnedAt ?? r.ReservedAt);

        var totalElements = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalElements / (double)size);

        var pageItems = await query
            .Skip(page * size)
            .Take(size)
            .Select(r => new HistoryEntryDto
            {
                ReservationId = r.ReservationId,
                BookTitle = r.BookTitle,
                BookAuthor = r.BookAuthor,
                ReservedAt = r.ReservedAt,
                CheckedOutAt = r.CheckedOutAt,
                ReturnedAt = r.ReturnedAt,
                DueDate = r.DueDate,
                Status = FormatStatus(r.Status),
                WasLate = r.ReturnedAt.HasValue && r.DueDate.HasValue && r.ReturnedAt.Value > r.DueDate.Value
            })
            .ToListAsync();

        return Ok(new
        {
            content = pageItems,
            page,
            size,
            totalElements,
            totalPages,
            last = page >= totalPages - 1
        });
    }
}