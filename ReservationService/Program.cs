using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReservationService.Data;
using ReservationService.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Digital Library Management System API", Version = "v1" });
});

builder.Services.AddDbContext<ReservationServiceContext>(options =>
    options.UseInMemoryDatabase("ReservationServiceDb"));

builder.Services.Configure<ServiceUrlsOptions>(builder.Configuration.GetSection("ServiceUrls"));

builder.Services.AddHttpClient("UserService", (sp, client) =>
{
    var urls = sp.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;
    client.BaseAddress = new Uri(urls.UserService);
});

builder.Services.AddHttpClient("CatalogService", (sp, client) =>
{
    var urls = sp.GetRequiredService<IOptions<ServiceUrlsOptions>>().Value;
    client.BaseAddress = new Uri(urls.CatalogService);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
