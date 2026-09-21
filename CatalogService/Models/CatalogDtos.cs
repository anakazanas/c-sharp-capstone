namespace CatalogService.Models;

public class BookSummaryDto
{
    public Guid BookId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int? PublicationYear { get; set; }
    public string? Description { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BookDetailDto : BookSummaryDto
{
    public string? Publisher { get; set; }
    public int? PageCount { get; set; }
    public string? Language { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PagedResult<T>
{
    public List<T> Content { get; set; } = new();
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
    public bool Last { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
