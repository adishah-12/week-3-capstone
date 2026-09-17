using Microsoft.EntityFrameworkCore;
using CatalogService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Catalog Service API", Version = "v1" });
});

builder.Services.AddDbContext<CatalogServiceContext>(options =>
    options.UseInMemoryDatabase("CatalogServiceDb"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogServiceContext>();
    await DataSeeder.SeedAsync(context);
}

app.Run();