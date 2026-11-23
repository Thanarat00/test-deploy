using System.ComponentModel.DataAnnotations;
using EmployeeManagement.Models;

namespace EmployeeManagement.ViewModels;

public class EmployeeViewModel : IValidatableObject
{
    private string _employeeCode = string.Empty;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private string? _department;

    [Display(Name = "รหัสพนักงาน")]
    [Required(ErrorMessage = "กรุณาระบุรหัสพนักงาน"), StringLength(20, ErrorMessage = "รหัสพนักงานต้องไม่เกิน 20 ตัวอักษร")]
    public string EmployeeCode
    {
        get => _employeeCode;
        set => _employeeCode = (value ?? string.Empty).Trim().ToUpperInvariant();
    }
    [Display(Name = "ชื่อ")]
    [Required(ErrorMessage = "กรุณาระบุชื่อ"), StringLength(100, ErrorMessage = "ชื่อต้องไม่เกิน 100 ตัวอักษร")]
    public string FirstName
    {
        get => _firstName;
        set => _firstName = (value ?? string.Empty).Trim();
    }
    [Display(Name = "นามสกุล")]
    [Required(ErrorMessage = "กรุณาระบุนามสกุล"), StringLength(100, ErrorMessage = "นามสกุลต้องไม่เกิน 100 ตัวอักษร")]
    public string LastName
    {
        get => _lastName;
        set => _lastName = (value ?? string.Empty).Trim();
    }
    [Display(Name = "อีเมล")]
    [Required(ErrorMessage = "กรุณาระบุอีเมล"), StringLength(150, ErrorMessage = "อีเมลต้องไม่เกิน 150 ตัวอักษร")]
    [EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    public string Email
    {
        get => _email;
        set => _email = (value ?? string.Empty).Trim().ToLowerInvariant();
    }
    [Display(Name = "แผนก"), StringLength(100, ErrorMessage = "แผนกต้องไม่เกิน 100 ตัวอักษร")]
    public string? Department
    {
        get => _department;
        set => _department = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    [Display(Name = "เงินเดือน (บาท)")]
    [Required(ErrorMessage = "กรุณาระบุเงินเดือน")]
    [Range(typeof(decimal), "0", "9999999999.99", ErrorMessage = "เงินเดือนต้องอยู่ระหว่าง 0 ถึง 9,999,999,999.99 บาท")]
    public decimal? Salary { get; set; }
    [Display(Name = "สถานะ Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Salary is decimal salary && decimal.Round(salary, 2) != salary)
            yield return new ValidationResult("เงินเดือนต้องมีทศนิยมไม่เกิน 2 ตำแหน่ง", [nameof(Salary)]);
    }

    public static EmployeeViewModel FromEmployee(Employee employee) => new()
    {
        EmployeeCode = employee.EmployeeCode, FirstName = employee.FirstName,
        LastName = employee.LastName, Email = employee.Email, Department = employee.Department,
        Salary = employee.Salary, IsActive = employee.IsActive
    };
}
