using EmployeeManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagement.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261123000100_InitialCreate")]
partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.22")
            .HasAnnotation("Relational:MaxIdentifierLength", 64)
            .HasAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity("EmployeeManagement.Models.Employee", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int")
                .HasAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime(6)");
            b.Property<string>("Department").HasMaxLength(100).HasColumnType("varchar(100)").HasCharSet("utf8mb4");
            b.Property<string>("Email").IsRequired().HasMaxLength(150).HasColumnType("varchar(150)").HasCharSet("utf8mb4");
            b.Property<string>("EmployeeCode").IsRequired().HasMaxLength(20).HasColumnType("varchar(20)").HasCharSet("utf8mb4");
            b.Property<string>("FirstName").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasCharSet("utf8mb4");
            b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("tinyint(1)").HasDefaultValue(true).HasSentinel(true);
            b.Property<string>("LastName").IsRequired().HasMaxLength(100).HasColumnType("varchar(100)").HasCharSet("utf8mb4");
            b.Property<decimal>("Salary").HasColumnType("decimal(12,2)");
            b.Property<DateTime?>("UpdatedAt").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasIndex("Email").IsUnique().HasDatabaseName("IX_Employees_Email");
            b.HasIndex("EmployeeCode").IsUnique().HasDatabaseName("IX_Employees_EmployeeCode");
            b.ToTable("Employees", table => table.HasCheckConstraint("CK_Employees_Salary_NonNegative", "`Salary` >= 0"));
        });
    }
}
