using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ReservationService.Models.Dtos;
using Xunit;

namespace ReservationService.Tests;

public class ReservationsControllerTests : IClassFixture<ReservationServiceTestFactory>
{
    private readonly ReservationServiceTestFactory _factory;
    private readonly HttpClient _client;

    public ReservationsControllerTests(ReservationServiceTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpClient ClientAs(Guid userId, string role)
    {
        var client = _factory.CreateClient();
        var token = TestJwtFactory.CreateToken(userId, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Guid SeedPatron()
    {
        var userId = Guid.NewGuid();
        _factory.UserMock.Users[userId] = new UserValidationResult
        {
            UserId = userId,
            Email = $"{userId}@test.com",
            FirstName = "Test",
            LastName = "Patron",
            Role = "PATRON",
            MembershipStatus = "ACTIVE"
        };
        return userId;
    }

    private Guid SeedBook(int availableCopies = 1, int totalCopies = 5)
    {
        var bookId = Guid.NewGuid();
        _factory.CatalogMock.Books[bookId] = new BookAvailabilityResult
        {
            BookId = bookId,
            Title = "Test Book",
            Author = "Test Author",
            AvailableCopies = availableCopies,
            TotalCopies = totalCopies
        };
        return bookId;
    }

    [Fact]
    public async Task Create_AvailableBook_Returns201AndDecrementsAvailability()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 2);
        var client = ClientAs(userId, "Patron");

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, _factory.CatalogMock.Books[bookId].AvailableCopies);
    }

    [Fact]
    public async Task Create_NoAvailableCopies_Returns400()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 0);
        var client = ClientAs(userId, "Patron");

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_SixthReservation_Returns400LimitExceeded()
    {
        var userId = SeedPatron();
        var client = ClientAs(userId, "Patron");

        for (int i = 0; i < 5; i++)
        {
            var bookId = SeedBook(availableCopies: 1);
            var res = await client.PostAsJsonAsync("/api/reservations", new { bookId });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        var sixthBookId = SeedBook(availableCopies: 1);
        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = sixthBookId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("RESERVATION_LIMIT_EXCEEDED", body!.Error);
    }

    [Fact]
    public async Task Checkout_AsPatron_Returns403()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 1);
        var client = ClientAs(userId, "Patron");

        var createResponse = await client.PostAsJsonAsync("/api/reservations", new { bookId });
        var reservation = await createResponse.Content.ReadFromJsonAsync<CreateReservationResponse>();

        var response = await client.PostAsJsonAsync(
            $"/api/reservations/{reservation!.ReservationId}/checkout", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_AsLibrarian_Returns200WithDueDate()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 1);
        var patronClient = ClientAs(userId, "Patron");
        var librarianClient = ClientAs(Guid.NewGuid(), "Librarian");

        var createResponse = await patronClient.PostAsJsonAsync("/api/reservations", new { bookId });
        var reservation = await createResponse.Content.ReadFromJsonAsync<CreateReservationResponse>();

        var response = await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation!.ReservationId}/checkout", new { notes = "Good" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.Equal("CHECKED_OUT", body!.Status);
        Assert.NotNull(body.DueDate);
    }

    [Fact]
    public async Task Checkout_AlreadyCheckedOut_Returns400()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 1);
        var patronClient = ClientAs(userId, "Patron");
        var librarianClient = ClientAs(Guid.NewGuid(), "Librarian");

        var createResponse = await patronClient.PostAsJsonAsync("/api/reservations", new { bookId });
        var reservation = await createResponse.Content.ReadFromJsonAsync<CreateReservationResponse>();

        await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation!.ReservationId}/checkout", new { notes = "Good" });

        var response = await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation.ReservationId}/checkout", new { notes = "Good" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Return_OnTime_ZeroLateFee()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 1);
        var patronClient = ClientAs(userId, "Patron");
        var librarianClient = ClientAs(Guid.NewGuid(), "Librarian");

        var createResponse = await patronClient.PostAsJsonAsync("/api/reservations", new { bookId });
        var reservation = await createResponse.Content.ReadFromJsonAsync<CreateReservationResponse>();

        await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation!.ReservationId}/checkout", new { notes = "Good" });

        var response = await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation.ReservationId}/return",
            new { condition = "Good", notes = "test" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReturnResponse>();
        Assert.Equal(0, body!.LateDays);
        Assert.Equal(0m, body.LateFee);
    }

    [Fact]
    public async Task Return_NoWaitlist_IncrementsAvailability()
    {
        var userId = SeedPatron();
        var bookId = SeedBook(availableCopies: 1);
        var patronClient = ClientAs(userId, "Patron");
        var librarianClient = ClientAs(Guid.NewGuid(), "Librarian");

        var createResponse = await patronClient.PostAsJsonAsync("/api/reservations", new { bookId });
        var reservation = await createResponse.Content.ReadFromJsonAsync<CreateReservationResponse>();

        Assert.Equal(0, _factory.CatalogMock.Books[bookId].AvailableCopies);

        await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation!.ReservationId}/checkout", new { notes = "Good" });

        await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservation.ReservationId}/return",
            new { condition = "Good", notes = "test" });

        Assert.Equal(1, _factory.CatalogMock.Books[bookId].AvailableCopies);
    }

    [Fact]
    public async Task WaitlistCascade_EligiblePatronWaiting_GetsAutoReservationInsteadOfIncrement()
    {
        var patronA = SeedPatron();
        var patronB = SeedPatron();
        var bookId = SeedBook(availableCopies: 1);

        var clientA = ClientAs(patronA, "Patron");
        var clientB = ClientAs(patronB, "Patron");
        var librarianClient = ClientAs(Guid.NewGuid(), "Librarian");

        var createResponse = await clientA.PostAsJsonAsync("/api/reservations", new { bookId });
        var reservationA = await createResponse.Content.ReadFromJsonAsync<CreateReservationResponse>();

        await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservationA!.ReservationId}/checkout", new { notes = "Good" });

        var joinResponse = await clientB.PostAsJsonAsync("/api/reservations/waitlist", new { bookId });
        joinResponse.EnsureSuccessStatusCode();

        await librarianClient.PostAsJsonAsync(
            $"/api/reservations/{reservationA.ReservationId}/return",
            new { condition = "Good", notes = "test" });

        Assert.Equal(0, _factory.CatalogMock.Books[bookId].AvailableCopies);

        var bActiveResponse = await clientB.GetAsync("/api/reservations");
        var bActive = await bActiveResponse.Content.ReadFromJsonAsync<ActiveReservationsResponse>();
        Assert.Equal(1, bActive!.TotalActive);
        Assert.Equal("RESERVED", bActive.Reservations[0].Status);
    }

    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
    }

    private class CreateReservationResponse
    {
        public Guid ReservationId { get; set; }
    }

    private class CheckoutResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
    }

    private class ReturnResponse
    {
        public int LateDays { get; set; }
        public decimal LateFee { get; set; }
    }

    private class ActiveReservationsResponse
    {
        public List<ActiveReservationItem> Reservations { get; set; } = new();
        public int TotalActive { get; set; }
    }

    private class ActiveReservationItem
    {
        public string Status { get; set; } = string.Empty;
    }
}