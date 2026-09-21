using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(CatalogServiceContext context)
    {
        if (await context.Books.AnyAsync())
        {
            return;
        }

        var books = new List<Book>
        {
            new() { Isbn = "978-0-13-468599-1", Title = "Clean Code", Author = "Robert C. Martin", Genre = "Technology", PublicationYear = 2008, Description = "A handbook of agile software craftsmanship", Publisher = "Prentice Hall", PageCount = 464, Language = "English", TotalCopies = 5, AvailableCopies = 2 },
            new() { Isbn = "978-0-13-475759-9", Title = "Refactoring", Author = "Martin Fowler", Genre = "Technology", PublicationYear = 2018, Description = "Improving the design of existing code", Publisher = "Addison-Wesley", PageCount = 448, Language = "English", TotalCopies = 3, AvailableCopies = 0 },
            new() { Isbn = "978-0-13-595705-9", Title = "The Pragmatic Programmer", Author = "Andrew Hunt", Genre = "Technology", PublicationYear = 1999, Description = "From journeyman to master", Publisher = "Addison-Wesley", PageCount = 352, Language = "English", TotalCopies = 4, AvailableCopies = 4 },
            new() { Isbn = "978-0-441-01359-3", Title = "Dune", Author = "Frank Herbert", Genre = "Science Fiction", PublicationYear = 1965, Description = "A epic tale of politics and prophecy", Publisher = "Ace Books", PageCount = 688, Language = "English", TotalCopies = 6, AvailableCopies = 1 },
            new() { Isbn = "978-0-553-29335-7", Title = "Foundation", Author = "Isaac Asimov", Genre = "Science Fiction", PublicationYear = 1951, Description = "The story of the fall and rise of civilizations", Publisher = "Bantam", PageCount = 255, Language = "English", TotalCopies = 2, AvailableCopies = 0 },
            new() { Isbn = "978-0-452-28423-4", Title = "1984", Author = "George Orwell", Genre = "Fiction", PublicationYear = 1949, Description = "A dystopian social science fiction novel", Publisher = "Secker & Warburg", PageCount = 328, Language = "English", TotalCopies = 10, AvailableCopies = 5 },
            new() { Isbn = "978-0-06-085052-4", Title = "Brave New World", Author = "Aldous Huxley", Genre = "Fiction", PublicationYear = 1932, Description = "A dystopian vision of a technologically advanced future", Publisher = "Chatto & Windus", PageCount = 311, Language = "English", TotalCopies = 3, AvailableCopies = 3 }
        };

        await context.Books.AddRangeAsync(books);
        await context.SaveChangesAsync();
    }
}
