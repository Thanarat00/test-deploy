using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmployeeManagement.Models;

public class Employee
{
    [Key]
    public int Id { get; set; }
    [Required, StringLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    [Required, StringLength(150), EmailAddress]
    public string Email { get; set; } = string.Empty;
    [StringLength(100)]
    public string? Department { get; set; }
    [Required, Column(TypeName = "decimal(12,2)")]
    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal Salary { get; set; }
    public bool IsActive { get; set; } = true;
    [Required, Column(TypeName = "datetime(6)")]
    public DateTime CreatedAt { get; set; }
    [Column(TypeName = "datetime(6)")]
    public DateTime? UpdatedAt { get; set; }
}
