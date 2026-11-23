using System.Globalization;
using EmployeeManagement.Data;
using EmployeeManagement.Services;
using EmployeeManagement.Services.Interfaces;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    // Ignored local configuration is shared with EF design-time tools.
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<ApplicationDbContext>(options => MySqlConfiguration.Configure(options, builder.Configuration));
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // Fixed culture keeps decimal binding consistent with HTML number inputs.
    var culture = new CultureInfo("en-US");
    options.DefaultRequestCulture = new RequestCulture(culture);
    options.SupportedCultures = [culture];
    options.SupportedUICultures = [culture];
    options.RequestCultureProviders.Clear();
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Employees}/{action=Index}/{id?}").WithStaticAssets();
// Schema changes are applied explicitly through dotnet ef database update.
app.Run();

public partial class Program;
