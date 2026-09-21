using CatalogService.Data;
using CatalogService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog/books")]
public class BooksController : ControllerBase
{
    private readonly CatalogServiceContext _context;

    public BooksController(CatalogServiceContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BookSummaryDto>>> GetBooks(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string sortBy = "title",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] string? query = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? isbn = null,
        [FromQuery] bool availableOnly = false)
    {
        var books = _context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            books = books.Where(b => b.Title.ToLower().Contains(term) || b.Author.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            books = books.Where(b => b.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            books = books.Where(b => b.Isbn == isbn);
        }

        if (availableOnly)
        {
            books = books.Where(b => b.AvailableCopies > 0);
        }

        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        books = sortBy.ToLowerInvariant() switch
        {
            "author" => descending ? books.OrderByDescending(b => b.Author) : books.OrderBy(b => b.Author),
            "publicationyear" => descending ? books.OrderByDescending(b => b.PublicationYear) : books.OrderBy(b => b.PublicationYear),
            _ => descending ? books.OrderByDescending(b => b.Title) : books.OrderBy(b => b.Title)
        };

        var totalElements = await books.CountAsync();
        var totalPages = size > 0 ? (int)Math.Ceiling(totalElements / (double)size) : 0;

        var pageItems = await books.Skip(page * size).Take(size).ToListAsync();

        var content = pageItems.Select(b => new BookSummaryDto
        {
            BookId = b.BookId,
            Isbn = b.Isbn,
            Title = b.Title,
            Author = b.Author,
            Genre = b.Genre,
            PublicationYear = b.PublicationYear,
            Description = b.Description,
            TotalCopies = b.TotalCopies,
            AvailableCopies = b.AvailableCopies,
            Status = b.Status
        }).ToList();

        return Ok(new PagedResult<BookSummaryDto>
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("{bookId:guid}")]
    public async Task<IActionResult> GetById(Guid bookId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book is null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "NOT_FOUND",
                Message = $"Book not found with ID: {bookId}"
            });
        }

        return Ok(new BookDetailDto
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = book.Status,
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }

    public record AvailabilityUpdate(int Delta);

    [HttpPut("{bookId:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid bookId, AvailabilityUpdate update)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book is null)
        {
            return NotFound();
        }

        book.AvailableCopies += update.Delta;
        await _context.SaveChangesAsync();

        return Ok(new { bookId = book.BookId, availableCopies = book.AvailableCopies });
    }
}
