using System.ComponentModel.DataAnnotations;
using System.Reflection;
using EmployeeManagement.Data;
using EmployeeManagement.Controllers;
using EmployeeManagement.Models;
using EmployeeManagement.Services.Interfaces;
using EmployeeManagement.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.Hosting;

var count = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
    count++;
}
List<ValidationResult> Validate(EmployeeViewModel model)
{
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    return results;
}
EmployeeViewModel ValidModel() => new()
{
    EmployeeCode = " emp001 ", FirstName = " สมชาย ", LastName = " ทดสอบ ",
    Email = " USER@EXAMPLE.COM ", Salary = 45000.25m, Department = "   "
};

var compiledViews = Assembly.GetExecutingAssembly().GetCustomAttributes<RazorCompiledItemAttribute>()
    .Select(item => item.Identifier).ToHashSet();
Check(new[] { "Index", "Create", "Edit", "Details", "Delete", "_EmployeeForm", "_EmployeeDetails" }
    .All(view => compiledViews.Contains($"/Views/Employees/{view}.cshtml"))
    && compiledViews.Contains("/Views/Shared/_Layout.cshtml"), "All actual Employee Razor views and layout compiled");

var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
var databaseSettings = DatabaseServerSettings.FromConfiguration(configuration);
Check(!databaseSettings.IsMariaDb && databaseSettings.Version == new Version(8, 0, 36), "MySQL remains the default engine");
configuration["Database:Provider"] = " mariadb ";
databaseSettings = DatabaseServerSettings.FromConfiguration(configuration);
Check(databaseSettings.IsMariaDb && databaseSettings.Version == new Version(10, 4, 32), "XAMPP MariaDB engine and default version");
configuration["Database:Provider"] = "MySql";
configuration["Database:ServerVersion"] = "8.4.0";
Check(DatabaseServerSettings.FromConfiguration(configuration).Version == new Version(8, 4, 0), "Explicit MySQL 8.4 version");
foreach (var (provider, version) in new[] { ("Other", "8.0.36"), ("MySql", "invalid"), ("MySql", "5.7.44"), ("MariaDb", "10.3.0") })
{
    configuration["Database:Provider"] = provider;
    configuration["Database:ServerVersion"] = version;
    var rejected = false;
    try { DatabaseServerSettings.FromConfiguration(configuration); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, $"Reject unsupported database settings: {provider}/{version}");
}

var model = ValidModel();
Check(Validate(model).Count == 0, "Valid form is accepted");
Check(model.EmployeeCode == "EMP001" && model.Email == "user@example.com", "Code/email normalized before validation");
Check(model.FirstName == "สมชาย" && model.Department is null, "Trim names and normalize empty department");
Check(model.IsActive, "New employee defaults to active");
Check(Validate(new EmployeeViewModel()).SelectMany(x => x.MemberNames).ToHashSet()
    .IsSupersetOf(["EmployeeCode", "FirstName", "LastName", "Email", "Salary"]), "All required fields checked");
model.FirstName = "   ";
Check(Validate(model).Any(x => x.MemberNames.Contains("FirstName")), "Whitespace-only names rejected");
model = ValidModel(); model.EmployeeCode = new string('A', 21);
Check(Validate(model).Any(x => x.MemberNames.Contains("EmployeeCode")), "Employee code length enforced");
model = ValidModel(); model.Email = "invalid";
Check(Validate(model).Any(x => x.MemberNames.Contains("Email")), "Malformed email rejected");
foreach (var salary in new[] { -0.01m, 10000000000m, 12.345m })
{
    model = ValidModel(); model.Salary = salary;
    Check(Validate(model).Any(x => x.MemberNames.Contains("Salary")), $"Invalid salary {salary} rejected");
}
foreach (var salary in new[] { 0m, 9999999999.99m })
{
    model = ValidModel(); model.Salary = salary;
    Check(Validate(model).Count == 0, $"Boundary salary {salary} accepted");
}
Check(new EmployeeListViewModel().FirstItem == 0 && new EmployeeListViewModel().TotalPages == 1, "Empty pagination");
var page = new EmployeeListViewModel { TotalCount = 26, Page = 3, PageSize = 10 };
Check(page.FirstItem == 21 && page.LastItem == 26 && page.TotalPages == 3, "Final page boundaries");
var overflowPage = new EmployeeListViewModel { TotalCount = int.MaxValue, Page = 42949673, PageSize = 50 };
Check(overflowPage.LastItem == int.MaxValue, "Pagination avoids integer overflow");

var fake = new FakeEmployees();
EmployeesController Controller()
{
    var http = new DefaultHttpContext();
    return new EmployeesController(fake)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempDataProvider())
    };
}
var controller = Controller();
Check(await controller.Details(999, default) is NotFoundResult, "Details missing employee returns 404");
Check(await controller.Edit(999, default) is NotFoundResult, "Edit missing employee returns 404");
Check(await controller.Delete(999, default) is NotFoundResult, "Delete confirmation missing employee returns 404");
Check(await controller.DeleteConfirmed(999, default) is NotFoundResult, "Delete POST missing employee returns 404");
controller = Controller(); controller.ModelState.AddModelError("Salary", "Invalid");
Check(await controller.Create(ValidModel(), default) is ViewResult && fake.Writes == 0, "Invalid form does not call service write");
fake.NextResult = EmployeeWriteResult.Invalid(new Dictionary<string, string[]> { ["Email"] = ["Duplicate"] });
controller = Controller();
Check(await controller.Create(ValidModel(), default) is ViewResult && !controller.ModelState.IsValid, "Duplicate error redisplays form");
fake.NextResult = EmployeeWriteResult.Success();
controller = Controller();
Check(await controller.Create(ValidModel(), default) is RedirectToActionResult { ActionName: "Index" }
    && controller.TempData.ContainsKey("Success"), "Successful create redirects with message");
fake.NextResult = EmployeeWriteResult.Missing();
Check(await Controller().Edit(999, ValidModel(), default) is NotFoundResult, "Concurrent deletion during edit returns 404");

var postMethods = typeof(EmployeesController).GetMethods().Where(m => m.IsDefined(typeof(HttpPostAttribute), true)).ToArray();
Check(postMethods.Length == 3 && postMethods.All(m => m.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true)), "All mutation actions require POST and antiforgery");
Console.WriteLine($"{count} checks passed; Razor views compiled by the Web SDK.");

sealed class MemoryTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

sealed class FakeEmployees : IEmployeeService
{
    public int Writes { get; private set; }
    public EmployeeWriteResult NextResult { get; set; } = EmployeeWriteResult.Success();
    public Task<EmployeeListViewModel> GetAllAsync(string? search, string? sort, int page, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult(new EmployeeListViewModel());
    public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<Employee?>(null);
    public Task<EmployeeWriteResult> CreateAsync(EmployeeViewModel model, CancellationToken cancellationToken = default)
    {
        Writes++;
        return Task.FromResult(NextResult);
    }
    public Task<EmployeeWriteResult> UpdateAsync(int id, EmployeeViewModel model, CancellationToken cancellationToken = default)
    {
        Writes++;
        return Task.FromResult(NextResult);
    }
    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
