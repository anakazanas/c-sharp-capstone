namespace ReservationService.Services;

public record BookInfo(Guid BookId, string Title, string Author, int AvailableCopies);

public interface ICatalogServiceClient
{
    Task<BookInfo?> GetBookAsync(Guid bookId);
    Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta);
}
