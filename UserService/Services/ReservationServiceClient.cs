using System.Net.Http.Json;
using System.Text.Json;

namespace UserService.Services;

public class ReservationServiceClient : IReservationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ReservationServiceClient> _logger;

    public ReservationServiceClient(IHttpClientFactory httpClientFactory, ILogger<ReservationServiceClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ReservationService");
        _logger = logger;
    }

    public async Task<ReservationStatistics?> GetStatisticsAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/reservations/statistics/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reservation Service returned {StatusCode} for user {UserId}", response.StatusCode, userId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ReservationStatistics>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reservation Service unavailable when fetching statistics for user {UserId}", userId);
            return null;
        }
    }
}
