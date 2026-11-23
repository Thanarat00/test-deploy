using EmployeeManagement.Models;

namespace EmployeeManagement.ViewModels;

public sealed class EmployeeListViewModel
{
    public IReadOnlyList<Employee> Employees { get; init; } = [];
    public string? Search { get; init; }
    public string Sort { get; init; } = "code";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstItem => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastItem => (int)Math.Min(Page * (long)PageSize, TotalCount);
}
