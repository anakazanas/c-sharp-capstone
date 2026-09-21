using System.ComponentModel.DataAnnotations;

namespace ReservationService.Models;

public class Reservation : IAuditable
{
    [Key]
    public Guid ReservationId { get; set; } = Guid.NewGuid();

    public Guid BookId { get; set; }
    public Guid UserId { get; set; }

    [Required]
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;

    [Required]
    public DateTime ReservedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnedAt { get; set; }

    public int RenewalCount { get; set; } = 0;

    public int? LateDays { get; set; }
    public decimal? LateFee { get; set; }
    public BookCondition? Condition { get; set; }
    public string? Notes { get; set; }

    [MaxLength(255)]
    public string BookTitle { get; set; } = string.Empty;

    [MaxLength(255)]
    public string BookAuthor { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
