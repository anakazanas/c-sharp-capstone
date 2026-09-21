namespace UserService.Services;

public record ReservationStatistics(int ActiveReservations, int BorrowingHistory);

public interface IReservationServiceClient
{
    Task<ReservationStatistics?> GetStatisticsAsync(Guid userId);
}
