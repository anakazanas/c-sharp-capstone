using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogService.Models;

public class Book : IAuditable
{
    [Key]
    public Guid BookId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(20)]
    public string Isbn { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Author { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Genre { get; set; } = string.Empty;

    public int? PublicationYear { get; set; }
    public string? Description { get; set; }

    [MaxLength(255)]
    public string? Publisher { get; set; }

    public int? PageCount { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    [Required]
    public int TotalCopies { get; set; } = 0;

    [Required]
    public int AvailableCopies { get; set; } = 0;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    public string Status => AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT";
}
