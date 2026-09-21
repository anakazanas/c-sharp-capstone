using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservationService.Data;
using ReservationService.Options;
using ReservationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Digital Library Management System API", Version = "v1" });
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseInMemoryDatabase("ReservationServiceDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseNpgsql(connectionString));
}

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

builder.Services.AddScoped<ICatalogServiceClient, CatalogServiceClient>();
builder.Services.AddScoped<IWaitlistCascadeService, WaitlistCascadeService>();
builder.Services.AddHostedService<WaitlistExpiryBackgroundService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();

    if (app.Environment.IsDevelopment())
    {
        await context.Database.EnsureCreatedAsync();
    }
    else
    {
        await context.Database.MigrateAsync();
    }
}

if (true) // Swagger enabled in all environments per Milestone 5
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
