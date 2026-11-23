using System.ComponentModel.DataAnnotations;
using EmployeeManagement.Data;
using EmployeeManagement.Models;
using EmployeeManagement.Services.Interfaces;
using EmployeeManagement.ViewModels;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace EmployeeManagement.Services;

public sealed class EmployeeService(ApplicationDbContext db) : IEmployeeService
{
    public async Task<EmployeeListViewModel> GetAllAsync(string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 150) search = search[..150];
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
        sort = sort is "code" or "code_desc" or "name" or "name_desc" or "email" or "email_desc"
            or "department" or "department_desc" or "salary" or "salary_desc" or "status" or "status_desc" ? sort : "code";
        var query = db.Employees.AsNoTracking();
        if (search is not null)
            query = query.Where(e => e.EmployeeCode.Contains(search) || e.FirstName.Contains(search)
                || e.LastName.Contains(search) || (e.FirstName + " " + e.LastName).Contains(search) || e.Email.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        IOrderedQueryable<Employee> ordered = sort switch
        {
            "code_desc" => query.OrderByDescending(e => e.EmployeeCode),
            "name" => query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName),
            "name_desc" => query.OrderByDescending(e => e.FirstName).ThenByDescending(e => e.LastName),
            "email" => query.OrderBy(e => e.Email),
            "email_desc" => query.OrderByDescending(e => e.Email),
            "department" => query.OrderBy(e => e.Department),
            "department_desc" => query.OrderByDescending(e => e.Department),
            "salary" => query.OrderBy(e => e.Salary),
            "salary_desc" => query.OrderByDescending(e => e.Salary),
            "status" => query.OrderBy(e => e.IsActive),
            "status_desc" => query.OrderByDescending(e => e.IsActive),
            _ => query.OrderBy(e => e.EmployeeCode)
        };
        var employees = await ordered.ThenBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new EmployeeListViewModel { Employees = employees, Search = search, Sort = sort, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<EmployeeWriteResult> CreateAsync(EmployeeViewModel model, CancellationToken cancellationToken = default)
    {
        var errors = await ValidateAsync(model, null, cancellationToken);
        if (errors.Count > 0) return EmployeeWriteResult.Invalid(errors);
        var employee = new Employee();
        Map(model, employee);
        db.Employees.Add(employee);
        return await SaveAsync(cancellationToken);
    }

    public async Task<EmployeeWriteResult> UpdateAsync(int id, EmployeeViewModel model, CancellationToken cancellationToken = default)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null) return EmployeeWriteResult.Missing();
        var errors = await ValidateAsync(model, id, cancellationToken);
        if (errors.Count > 0) return EmployeeWriteResult.Invalid(errors);
        Map(model, employee);
        // Also record UpdatedAt when an unchanged form is submitted.
        db.Entry(employee).State = EntityState.Modified;
        return await SaveAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null) return false;
        db.Employees.Remove(employee);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(EmployeeViewModel model, int? id, CancellationToken cancellationToken)
    {
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), validation, validateAllProperties: true);
        var errors = validation.SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (Member: member, Error: result.ErrorMessage ?? "ข้อมูลไม่ถูกต้อง")))
            .GroupBy(result => result.Member).ToDictionary(group => group.Key, group => group.Select(result => result.Error).ToArray());
        if (errors.Count > 0) return errors;
        if (await db.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode && (!id.HasValue || e.Id != id.Value), cancellationToken))
            errors[nameof(model.EmployeeCode)] = ["รหัสพนักงานนี้มีอยู่แล้ว"];
        if (await db.Employees.AnyAsync(e => e.Email == model.Email && (!id.HasValue || e.Id != id.Value), cancellationToken))
            errors[nameof(model.Email)] = ["อีเมลนี้มีอยู่แล้ว"];
        return errors;
    }

    private async Task<EmployeeWriteResult> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return EmployeeWriteResult.Success();
        }
        catch (DbUpdateConcurrencyException) { return EmployeeWriteResult.Missing(); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            // Handle races after the pre-check using the authoritative MySQL unique indexes.
            var message = exception.InnerException.Message;
            var key = message.Contains("IX_Employees_EmployeeCode", StringComparison.OrdinalIgnoreCase) ? nameof(EmployeeViewModel.EmployeeCode)
                : message.Contains("IX_Employees_Email", StringComparison.OrdinalIgnoreCase) ? nameof(EmployeeViewModel.Email) : string.Empty;
            var error = key == nameof(EmployeeViewModel.EmployeeCode) ? "รหัสพนักงานนี้มีอยู่แล้ว"
                : key == nameof(EmployeeViewModel.Email) ? "อีเมลนี้มีอยู่แล้ว" : "รหัสพนักงานหรืออีเมลนี้มีอยู่แล้ว กรุณาตรวจสอบข้อมูล";
            return EmployeeWriteResult.Invalid(new Dictionary<string, string[]> { [key] = [error] });
        }
    }

    private static void Map(EmployeeViewModel model, Employee employee)
    {
        employee.EmployeeCode = model.EmployeeCode;
        employee.FirstName = model.FirstName;
        employee.LastName = model.LastName;
        employee.Email = model.Email;
        employee.Department = model.Department;
        employee.Salary = model.Salary!.Value;
        employee.IsActive = model.IsActive;
    }
}
