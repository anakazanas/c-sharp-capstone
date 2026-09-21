using System.Net.Http.Json;
using System.Text.Json;

namespace ReservationService.Services;

public class CatalogServiceClient : ICatalogServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogServiceClient> _logger;

    public CatalogServiceClient(IHttpClientFactory httpClientFactory, ILogger<CatalogServiceClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("CatalogService");
        _logger = logger;
    }

    public async Task<BookInfo?> GetBookAsync(Guid bookId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/catalog/books/{bookId}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<BookInfo>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog Service unavailable when fetching book {BookId}", bookId);
            return null;
        }
    }

    public async Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/catalog/books/{bookId}/availability", new { delta });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog Service unavailable when updating availability for book {BookId}", bookId);
            return false;
        }
    }
}
