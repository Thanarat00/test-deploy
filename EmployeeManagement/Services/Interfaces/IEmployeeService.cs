using EmployeeManagement.Models;
using EmployeeManagement.ViewModels;

namespace EmployeeManagement.Services.Interfaces;

public interface IEmployeeService
{
    Task<EmployeeListViewModel> GetAllAsync(string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeWriteResult> CreateAsync(EmployeeViewModel model, CancellationToken cancellationToken = default);
    Task<EmployeeWriteResult> UpdateAsync(int id, EmployeeViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record EmployeeWriteResult(bool Succeeded, bool NotFound, IReadOnlyDictionary<string, string[]> Errors)
{
    public static EmployeeWriteResult Success() => new(true, false, new Dictionary<string, string[]>());
    public static EmployeeWriteResult Missing() => new(false, true, new Dictionary<string, string[]>());
    public static EmployeeWriteResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(false, false, errors);
}
