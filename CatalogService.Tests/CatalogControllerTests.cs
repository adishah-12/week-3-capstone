using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CatalogService.Tests;

public class CatalogControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CatalogControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBooks_ReturnsDefaultPagination()
    {
        var response = await _client.GetAsync("/api/catalog/books");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResult>();

        Assert.NotNull(body);
        Assert.Equal(0, body!.Page);
        Assert.Equal(20, body.Size);
        Assert.True(body.TotalElements > 0);
    }

    [Fact]
    public async Task GetBooks_FilterByGenre_ReturnsOnlyMatchingGenre()
    {
        var response = await _client.GetAsync("/api/catalog/books?genre=Technology");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResult>();

        Assert.NotNull(body);
        Assert.All(body!.Content, b => Assert.Equal("Technology", b.Genre));
    }

    [Fact]
    public async Task GetBooks_AvailableOnly_ReturnsOnlyBooksWithCopies()
    {
        var response = await _client.GetAsync("/api/catalog/books?availableOnly=true");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResult>();

        Assert.NotNull(body);
        Assert.All(body!.Content, b => Assert.True(b.AvailableCopies > 0));
    }

    [Fact]
    public async Task GetBooks_SortByTitleDescending_ReturnsDescendingOrder()
    {
        var response = await _client.GetAsync("/api/catalog/books?sortBy=title&sortOrder=desc");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResult>();

        Assert.NotNull(body);
        var titles = body!.Content.Select(b => b.Title).ToList();
        var sorted = titles.OrderByDescending(t => t).ToList();
        Assert.Equal(sorted, titles);
    }

    [Fact]
    public async Task GetBookById_ExistingBook_ReturnsFullDetail()
    {
        var listResponse = await _client.GetAsync("/api/catalog/books");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResult>();
        var bookId = list!.Content.First().BookId;

        var response = await _client.GetAsync($"/api/catalog/books/{bookId}");
        response.EnsureSuccessStatusCode();

        var book = await response.Content.ReadFromJsonAsync<BookDetail>();

        Assert.NotNull(book);
        Assert.Equal(bookId, book!.BookId);
    }

    [Fact]
    public async Task GetBookById_NonExistentBook_Returns404()
    {
        var response = await _client.GetAsync($"/api/catalog/books/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBooks_EmptySearchQuery_ReturnsEmptyContentNotError()
    {
        var response = await _client.GetAsync("/api/catalog/books?query=zzz_no_book_has_this_zzz");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedResult>();

        Assert.NotNull(body);
        Assert.Empty(body!.Content);
    }

    private class PagedResult
    {
        public List<BookListItem> Content { get; set; } = new();
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalElements { get; set; }
        public int TotalPages { get; set; }
        public bool Last { get; set; }
    }

    private class BookListItem
    {
        public Guid BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public int AvailableCopies { get; set; }
    }

    private class BookDetail
    {
        public Guid BookId { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}