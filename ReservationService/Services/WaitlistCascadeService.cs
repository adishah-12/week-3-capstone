using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models.Entities;
using ReservationService.Services.HttpClients;

namespace ReservationService.Services;

public static class CascadeService
{
    private const int MaxActiveReservations = 5;
    private const int ExpiryDays = 7;

    public static async Task ReleaseOrCascadeAsync(
        ReservationServiceContext db,
        ICatalogServiceClient catalogClient,
        Guid bookId)
    {
        while (true)
        {
            var nextInLine = await db.WaitlistEntries
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (nextInLine is null)
            {
                await catalogClient.UpdateAvailabilityAsync(bookId, 1);
                return;
            }

            var activeCount = await db.Reservations.CountAsync(r =>
                r.UserId == nextInLine.UserId &&
                (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

            if (activeCount >= MaxActiveReservations)
            {
                nextInLine.Status = WaitlistStatus.Expired;
                await db.SaveChangesAsync();
                continue;
            }

            var claimedAt = DateTime.UtcNow;
            var newReservation = new Reservation
            {
                ReservationId = Guid.NewGuid(),
                BookId = bookId,
                UserId = nextInLine.UserId,
                Status = ReservationStatus.Reserved,
                ReservedAt = claimedAt,
                ExpiresAt = claimedAt.AddDays(ExpiryDays),
                BookTitle = nextInLine.BookTitle,
                BookAuthor = nextInLine.BookAuthor
            };
            db.Reservations.Add(newReservation);

            nextInLine.Status = WaitlistStatus.Notified;
            nextInLine.NotifiedAt = claimedAt;
            nextInLine.ClaimDeadline = claimedAt.AddHours(48);
            nextInLine.ResultingReservationId = newReservation.ReservationId;

            await db.SaveChangesAsync();
            return;
        }
    }
}