using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models.Dtos;
using ReservationService.Models.Entities;
using ReservationService.Services.HttpClients;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private const int MaxActiveReservations = 5;
    private const int ExpiryDays = 7;

    private readonly ReservationServiceContext _db;
    private readonly UserServiceClient _userClient;
    private readonly CatalogServiceClient _catalogClient;

    public ReservationsController(
        ReservationServiceContext db,
        UserServiceClient userClient,
        CatalogServiceClient catalogClient)
    {
        _db = db;
        _userClient = userClient;
        _catalogClient = catalogClient;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue("userId")!);

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
            status = reservation.Status.ToString().ToUpper(),
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
            Status = r.Status.ToString().ToUpper(),
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
}