using EmployeeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci");
        var employee = modelBuilder.Entity<Employee>();
        employee.ToTable("Employees", table =>
            table.HasCheckConstraint("CK_Employees_Salary_NonNegative", "`Salary` >= 0"));
        employee.HasKey(e => e.Id);
        employee.Property(e => e.Id).ValueGeneratedOnAdd();
        employee.Property(e => e.EmployeeCode).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
        employee.Property(e => e.FirstName).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        employee.Property(e => e.LastName).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        employee.Property(e => e.Email).HasColumnType("varchar(150)").HasMaxLength(150).IsRequired();
        employee.Property(e => e.Department).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired(false);
        employee.Property(e => e.Salary).HasColumnType("decimal(12,2)").IsRequired();
        // Sentinel true lets EF insert false explicitly instead of using the database default.
        employee.Property(e => e.IsActive).HasColumnType("tinyint(1)").HasDefaultValue(true).HasSentinel(true);
        employee.Property(e => e.CreatedAt).HasColumnType("datetime(6)").IsRequired();
        employee.Property(e => e.UpdatedAt).HasColumnType("datetime(6)").IsRequired(false);
        employee.HasIndex(e => e.EmployeeCode).IsUnique().HasDatabaseName("IX_Employees_EmployeeCode");
        employee.HasIndex(e => e.Email).IsUnique().HasDatabaseName("IX_Employees_Email");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Employee>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
