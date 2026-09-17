using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReservationService.Data;
using ReservationService.Services.HttpClients;
using ReservationService.Tests.Mocks;

namespace ReservationService.Tests;

public class ReservationServiceTestFactory : WebApplicationFactory<Program>
{
    public MockCatalogServiceClient CatalogMock { get; } = new();
    public MockUserServiceClient UserMock { get; } = new();

    private readonly string _dbName = $"TestDb-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ReservationServiceContext>>();
            services.AddDbContext<ReservationServiceContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.RemoveAll<ICatalogServiceClient>();
            services.AddSingleton<ICatalogServiceClient>(CatalogMock);

            services.RemoveAll<IUserServiceClient>();
            services.AddSingleton<IUserServiceClient>(UserMock);
        });
    }
}