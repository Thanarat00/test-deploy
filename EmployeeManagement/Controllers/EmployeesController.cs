using EmployeeManagement.Services.Interfaces;
using EmployeeManagement.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Controllers;

public class EmployeesController(IEmployeeService employees) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? sort, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        View(await employees.GetAllAsync(search, sort, page, pageSize, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(id, cancellationToken);
        return employee is null ? NotFound() : View(employee);
    }
    [HttpGet]
    public IActionResult Create() => View(new EmployeeViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await employees.CreateAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }
        TempData["Success"] = "เพิ่มข้อมูลพนักงานเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(id, cancellationToken);
        if (employee is null) return NotFound();
        ViewData["EmployeeId"] = id;
        return View(EmployeeViewModel.FromEmployee(employee));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmployeeViewModel model, CancellationToken cancellationToken)
    {
        ViewData["EmployeeId"] = id;
        if (!ModelState.IsValid)
        {
            if (await employees.GetByIdAsync(id, cancellationToken) is null) return NotFound();
            return View(model);
        }
        var result = await employees.UpdateAsync(id, model, cancellationToken);
        if (result.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(model);
        }
        TempData["Success"] = "แก้ไขข้อมูลพนักงานเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(id, cancellationToken);
        return employee is null ? NotFound() : View(employee);
    }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        if (!await employees.DeleteAsync(id, cancellationToken)) return NotFound();
        TempData["Success"] = "ลบข้อมูลพนักงานเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }
    private void AddErrors(EmployeeWriteResult result)
    {
        foreach (var (field, messages) in result.Errors)
            foreach (var message in messages)
                ModelState.AddModelError(field, message);
    }
}
