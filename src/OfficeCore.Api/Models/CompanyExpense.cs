using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum ExpenseCategory
{
    GeneralExpense,
    EquipmentPurchase
}

public class CompanyExpense
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public required string Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; }

    public DateTime DateIncurred { get; set; }

    [MaxLength(1000)]
    public string? ReceiptUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
