using EmployeeManagement.Data;
using EmployeeManagement.Models;
using EmployeeManagement.Services;
using EmployeeManagement.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

var connection = Environment.GetEnvironmentVariable("EMPLOYEE_TEST_MYSQL")
    ?? throw new InvalidOperationException("Set EMPLOYEE_TEST_MYSQL; tests create and remove their own uniquely named database.");
var databaseName = "EmployeeManagementTests_" + Guid.NewGuid().ToString("N");
var connectionBuilder = new MySqlConnectionStringBuilder(connection) { Database = databaseName, DateTimeKind = MySqlDateTimeKind.Utc };
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = connectionBuilder.ConnectionString,
    ["Database:Provider"] = Environment.GetEnvironmentVariable("EMPLOYEE_TEST_PROVIDER") ?? "MySql",
    ["Database:ServerVersion"] = Environment.GetEnvironmentVariable("EMPLOYEE_TEST_SERVER_VERSION")
}).Build();
var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
MySqlConfiguration.Configure(optionsBuilder, configuration);
var options = optionsBuilder.Options;
await using var db = new ApplicationDbContext(options);
var checks = 0;
void Check(bool passed, string message)
{
    if (!passed) throw new InvalidOperationException("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
    checks++;
}
EmployeeViewModel Model(string code = "EMP001", string email = "employee@example.com") => new()
{
    EmployeeCode = code, FirstName = "สมชาย", LastName = "ทดสอบ", Email = email,
    Department = "วิศวกรรม", Salary = 12000.50m, IsActive = false
};

try
{
    await db.Database.MigrateAsync();
    Check((await db.Database.GetAppliedMigrationsAsync()).Contains("20261123000100_InitialCreate"), "InitialCreate applied to a real MySQL database");
    Check(!db.Database.HasPendingModelChanges(), "Snapshot matches current model for future migrations");
    await db.Database.MigrateAsync();
    Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 1, "Migration can be applied repeatedly");

    var service = new EmployeeService(db);
    Check((await service.CreateAsync(Model())).Succeeded, "Create employee");
    var employee = await db.Employees.AsNoTracking().SingleAsync();
    var employeeId = employee.Id;
    var createdAt = employee.CreatedAt;
    Check(employee.Id > 0 && employee.CreatedAt != default && employee.UpdatedAt is null, "Identity and creation timestamps");
    Check(!employee.IsActive && employee.Salary == 12000.50m, "Inactive false and exact decimal survive insertion");
    Check(employee.FirstName == "สมชาย", "UTF8 Thai data roundtrip");
    Check((await service.CreateAsync(Model("emp001", "other@example.com"))).Errors.ContainsKey("EmployeeCode"), "Code uniqueness is case insensitive");
    Check((await service.CreateAsync(Model("EMP002", "EMPLOYEE@EXAMPLE.COM"))).Errors.ContainsKey("Email"), "Email uniqueness is case insensitive");
    var invalidSalary = Model("BAD", "bad@example.com"); invalidSalary.Salary = 10.123m;
    Check((await service.CreateAsync(invalidSalary)).Errors.ContainsKey("Salary"), "Service rejects excess decimal places");

    db.ChangeTracker.Clear();
    var edit = Model(); edit.FirstName = "สมหญิง"; edit.IsActive = true;
    Check((await service.UpdateAsync(employeeId, edit)).Succeeded, "Edit retains own unique code/email");
    employee = (await service.GetByIdAsync(employeeId))!;
    Check(employee.FirstName == "สมหญิง" && employee.IsActive && employee.UpdatedAt.HasValue && employee.CreatedAt == createdAt, "Update data and timestamp; preserve CreatedAt");

    for (var i = 2; i <= 26; i++)
        db.Employees.Add(new Employee { EmployeeCode = $"EMP{i:000}", FirstName = "Test", LastName = i.ToString(), Email = $"test{i}@example.com", Salary = i * 100m });
    await db.SaveChangesAsync();
    var page = await service.GetAllAsync(null, "code", 3, 10);
    Check(page.TotalCount == 26 && page.TotalPages == 3 && page.Employees.Count == 6, "Pagination across 26 employees");
    page = await service.GetAllAsync(null, "salary_desc", 1, 10);
    Check(page.Employees.First().Id == employeeId, "Salary sorting executes in MySQL");
    page = await service.GetAllAsync("สมหญิง ทดสอบ", "name", 1, 10);
    Check(page.TotalCount == 1, "Search full Thai name");
    page = await service.GetAllAsync("EMP026", "code", 1, 10);
    Check(page.TotalCount == 1, "Search employee code");
    page = await service.GetAllAsync("test26@example.com", "code", 1, 10);
    Check(page.TotalCount == 1, "Search email");
    page = await service.GetAllAsync("%", "code", 1, 10);
    Check(page.TotalCount == 0, "Search percent sign is literal");
    page = await service.GetAllAsync(null, "injection;", int.MaxValue, int.MaxValue);
    Check(page.Sort == "code" && page.PageSize == 10 && page.Page == 3, "Bound untrusted paging and whitelist sort");

    // Bypass the service to prove the database itself enforces uniqueness.
    db.ChangeTracker.Clear();
    db.Employees.Add(new Employee { EmployeeCode = "EMP001", FirstName = "Race", LastName = "Test", Email = "race@example.com", Salary = 0 });
    var duplicateRejected = false;
    try { await db.SaveChangesAsync(); }
    catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 }) { duplicateRejected = true; }
    Check(duplicateRejected, "Unique index rejects competing duplicate inserts");
    db.ChangeTracker.Clear();

    Check(await service.DeleteAsync(employeeId), "Delete existing employee");
    Check(await service.GetByIdAsync(employeeId) is null && !await service.DeleteAsync(employeeId), "Deleted employee returns missing");
    Check((await service.UpdateAsync(employeeId, edit)).NotFound, "Edit deleted employee returns missing");
    Console.WriteLine($"{checks} MySQL checks passed.");
}
finally
{
    // Only the unique database generated by this process may be removed.
    if (connectionBuilder.Database != databaseName || !databaseName.StartsWith("EmployeeManagementTests_", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing to remove an unexpected database.");
    await db.Database.EnsureDeletedAsync();
}
