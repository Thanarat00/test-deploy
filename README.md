# Employee Management System

ASP.NET Core MVC บน **.NET 10** สำหรับจัดการพนักงาน พร้อม Razor ภาษาไทย, Bootstrap 5.3.3 แบบ local, Vanilla JavaScript และ Service Layer ใช้ MySQL 8 หรือ MariaDB ใน XAMPP ผ่าน EF Core Code First Migration

## เวอร์ชันและความเข้ากันได้

| ส่วนประกอบ | เวอร์ชัน |
| --- | --- |
| Target framework | `net10.0` |
| EF Core / EF Core Design / dotnet-ef | `9.0.22` |
| Pomelo.EntityFrameworkCore.MySql | `9.0.0` |
| MySqlConnector | `2.6.1` (pin โดยตรงเพื่อแก้ GHSA-473q-m89c-ghf8) |
| MySQL | 8.0.16+ หรือ 8.4; ค่าเริ่มต้นสำหรับ SQL generation คือ 8.0.36 |
| MariaDB สำหรับ XAMPP local | 10.4.32 ที่ตรวจพบในเครื่องนี้ |

แยกเวอร์ชัน .NET runtime ออกจาก EF Core: Pomelo 9.0.0 รองรับ EF Core **9.0.x** และ .NET 8 ขึ้นไป จึงใช้กับแอป `net10.0` ได้ ห้ามอัปเกรด EF Core เป็น 10 โดยยังใช้ Pomelo 9 อยู่ ข้อมูลอ้างอิง: [Pomelo 9 compatibility](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/tree/9.0.0#compatibility), [EF Core package](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/9.0.22), [EF CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)

MySQL 8.0.16+ จำเป็นสำหรับการบังคับใช้ CHECK constraint ของเงินเดือน ใช้ collation `utf8mb4_unicode_ci` ที่มีทั้งใน MySQL และ MariaDB ทำให้ค้นหาและตรวจข้อมูลซ้ำโดยไม่แยกตัวพิมพ์ใหญ่/เล็ก รองรับข้อความภาษาไทย Model, migration และ snapshot ใช้ collation เดียวกัน

## เปิดกับ XAMPP เครื่องนี้

เปิด **MySQL** ใน XAMPP ก่อน ตัวเว็บ .NET ไม่ต้องใช้ Apache; Apache ที่พอร์ต 8080 ใช้เปิด phpMyAdmin แยกได้

จาก repository root รัน:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/start-local.ps1
```

หรือดับเบิลคลิก `start-local.cmd` แล้วเปิด `http://localhost:5034` หลังเห็นข้อความ `Now listening on` สคริปต์จะ:

1. ตรวจ server ที่ `127.0.0.1:3306` ด้วย client ของ XAMPP (`C:/xampp/mysql/bin/mysql.exe`)
2. สร้างฐานข้อมูลว่างและบัญชีเฉพาะแอป พร้อมรหัสผ่านสุ่ม โดยใช้ root ของ XAMPP ที่ไม่มีรหัสผ่านตามการตั้งค่าของเครื่องนี้
3. บันทึก connection string และชนิด server ลง `EmployeeManagement/appsettings.Local.json` ซึ่งถูก ignore และไม่รวมตอน publish; รันซ้ำจะใช้บัญชีเดิม
4. Restore packages/tools, หยุด EmployeeManagement ที่รันจาก `bin` ของโปรเจกต์นี้, build, ตรวจ snapshot, ใช้ `dotnet ef database update` แล้วเปิดแอป

สคริปต์ไม่สร้างตารางด้วย SQL; ตารางและ indexes มาจาก EF Migrations เท่านั้น หาก root ของ XAMPP มีรหัสผ่าน ให้เตรียม database/account และไฟล์ local เองตามตัวอย่างด้านล่างก่อนใช้คำสั่ง .NET โดยตรง

ตัวเลือกเพิ่มเติม:

```powershell
# เตรียมเฉพาะ database/account/local configuration ไม่ดาวน์โหลดแพ็กเกจและไม่เปิดเว็บ
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/start-local.ps1 -ConfigureOnly

# Restore/build/migrate แต่ไม่เปิดเว็บ
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/start-local.ps1 -SkipRun
```

ตัวอย่างส่วนกำหนด engine สำหรับ MariaDB ใน local settings (connection string ต้องใช้บัญชีของคุณ):

```json
{
  "Database": {
    "Provider": "MariaDb",
    "ServerVersion": "10.4.32"
  }
}
```

ค่าเริ่มต้นใน `appsettings.json` ยังคงเป็น `MySql`/`8.0.36` สำหรับสเปกเดิม ทั้ง runtime และ EF design-time โหลด local settings เฉพาะ Development และให้ user secrets/environment variables/command-line arguments override ได้

## เริ่มใช้งาน

ติดตั้ง .NET 10 SDK และเตรียม MySQL 8 ตามเวอร์ชันข้างต้น คำสั่งต่อไปนี้ใช้ PowerShell จากโฟลเดอร์ repository

```powershell
dotnet restore EmployeeManagement.sln
dotnet tool restore
cd EmployeeManagement
```

เตรียมฐานข้อมูลว่างและบัญชี MySQL ตัวอย่างสำหรับ local development (รันใน MySQL ด้วยบัญชีผู้ดูแล เปลี่ยนรหัสผ่านตัวอย่าง):

```sql
CREATE DATABASE EmployeeManagementDb
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'employee_app'@'localhost' IDENTIFIED BY 'REPLACE_WITH_YOUR_PASSWORD';
GRANT ALL PRIVILEGES ON EmployeeManagementDb.* TO 'employee_app'@'localhost';
```

กำหนด connection string ด้วย user secrets; `UserSecretsId` มีอยู่ใน csproj แล้ว ไม่ต้องรัน `user-secrets init` อีก

```powershell
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=localhost;Port=3306;Database=EmployeeManagementDb;User=employee_app;Password=REPLACE_WITH_YOUR_PASSWORD;CharSet=utf8mb4;DateTimeKind=Utc'
```

หรือกำหนด environment variable (ใช้ได้ทั้ง runtime และ EF tools):

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Port=3306;Database=EmployeeManagementDb;User=employee_app;Password=REPLACE_WITH_YOUR_PASSWORD;CharSet=utf8mb4;DateTimeKind=Utc'
$env:Database__ServerVersion = '8.0.36'
$env:Database__Provider = 'MySql'
```

สำหรับ MySQL 8.4 ตั้ง `Database__ServerVersion` ให้ตรงเวอร์ชัน server จริง ค่าใน appsettings ไม่มีรหัสผ่านและเป็นเพียงค่าเริ่มต้นสำหรับเครื่อง local

โครงการมี `InitialCreate` และ Model Snapshot มาให้แล้ว **ไม่ต้องสร้าง InitialCreate ซ้ำ** ใช้:

```powershell
# ให้ local dotnet-ef ที่ target .NET 8 รันได้บนเครื่องที่มีเฉพาะ .NET 10
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet ef database update
dotnet run --launch-profile http
```

เปิด `http://localhost:5034` จะไปหน้า Employees อัตโนมัติ หากใช้ HTTPS:

```powershell
dotnet dev-certs https --trust
dotnet run --launch-profile https
```

เปิด `https://localhost:7258` หลัง trust certificate

ใช้ local tool manifest ที่ pin เวอร์ชันไว้แล้ว หากต้องการใช้ global tool แทน:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.22 --allow-roll-forward
```

## โครงสร้าง

```text
EmployeeManagement/
  Controllers/EmployeesController.cs
  Models/Employee.cs
  ViewModels/EmployeeViewModel.cs
  ViewModels/EmployeeListViewModel.cs
  Data/ApplicationDbContext.cs
  Data/ApplicationDbContextFactory.cs
  Data/MySqlConfiguration.cs
  Data/DatabaseServerSettings.cs
  Services/Interfaces/IEmployeeService.cs
  Services/EmployeeService.cs
  Migrations/20261123000100_InitialCreate.cs
  Migrations/20261123000100_InitialCreate.Designer.cs
  Migrations/ApplicationDbContextModelSnapshot.cs
  Views/Employees/{Index,Create,Edit,Details,Delete}.cshtml
  Views/Shared/_Layout.cshtml
  wwwroot/{css,js,lib/bootstrap}/
tests/
  EmployeeManagement.Checks/         # ไม่ต้องใช้ MySQL หรือ NuGet packages เพิ่ม
  EmployeeManagement.MySqlChecks/   # ทดสอบ provider และ migrations กับ MySQL จริง
scripts/test.ps1
scripts/start-local.ps1
start-local.cmd
```

Controller จัดการ HTTP และ ModelState → Service ตรวจ validation/ข้อมูลซ้ำและจัดการ CRUD → DbContext กำหนด schema และ timestamps → Pomelo เชื่อม MySQL โดยใช้ DI แบบ scoped

## พฤติกรรมของระบบ

- Create/Edit ใช้ ViewModel แยกจาก Entity จึงไม่มีช่องรับ `Id`, `CreatedAt` หรือ `UpdatedAt` จาก form; ID ของ Edit มาจาก URL
- Client ตรวจ required, ความยาว, email, เงินเดือนขั้นต่ำ/สูงสุดและทศนิยม ด้วย HTML constraints และ Vanilla JavaScript; ปิด JavaScript แล้วยังมี HTML validation และ Server validation
- Server ตรวจ Data Annotations และ IValidatableObject ทั้งใน MVC และ Service; เงินเดือนต้องอยู่ในช่วง 0–9,999,999,999.99 และไม่เกิน 2 ตำแหน่ง จึงไม่ปล่อยให้ MySQL ปัดเศษเงียบ ๆ
- รหัส trim/แปลงเป็นตัวพิมพ์ใหญ่, email trim/ตัวพิมพ์เล็ก, ชื่อ trim, แผนกว่างเก็บเป็น NULL
- ตรวจรหัส/email ซ้ำก่อนบันทึก และแปลง MySQL error 1062 จาก Unique Index เป็นข้อความใน form เมื่อมีการบันทึกพร้อมกัน
- ค้นหารหัส ชื่อ นามสกุล ชื่อเต็ม และ email; เรียงลำดับรหัส/ชื่อ/email/แผนก/เงินเดือน/สถานะทั้งสองทิศทาง; ทุกลำดับใช้ Id เป็นตัวตัดสินเมื่อค่าเท่ากัน
- แสดง 10/25/50 รายการต่อหน้า; จำกัดค่าจาก query string และคงเงื่อนไขค้นหา/เรียงลำดับเมื่อเปลี่ยนหน้า
- `IsActive` มี default TRUE ที่ฐานข้อมูล และตั้ง EF sentinel เพื่อให้การสร้างพนักงาน Inactive บันทึก FALSE ได้
- `CreatedAt`/`UpdatedAt` เป็น `datetime(6)` เก็บ UTC ใน DbContext; การแก้ไขไม่เปลี่ยน CreatedAt
- Delete GET แสดงหน้ายืนยัน; ลบจริงผ่าน POST พร้อม Anti-Forgery Token เท่านั้น; ไม่พบรายการคืน HTTP 404 รวมกรณีถูกลบระหว่างเปิด form
- การบันทึกสำเร็จใช้ Post/Redirect/Get และ TempData success message
- Edit พร้อมกันใช้ข้อมูลจากการบันทึกล่าสุด (ไม่มี row version ใน schema นี้)
- ไม่มีการสร้าง/เปลี่ยน schema ตอนเปิดแอป; ใช้ EF Migrations เท่านั้น ไม่ใช้ EnsureCreated

## เปลี่ยน Database Schema ในอนาคต

ทำจากโฟลเดอร์ `EmployeeManagement` และใช้ EF tools เวอร์ชันที่ pin ไว้ ตรวจ `Up`, `Down` และ snapshot ก่อนใช้กับฐานข้อมูล

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet ef migrations has-pending-model-changes

# หลังแก้ Model และ Fluent API
dotnet ef migrations add AddEmployeePhone
dotnet ef migrations script InitialCreate AddEmployeePhone --output ../artifacts/AddEmployeePhone.sql
dotnet ef database update
```

สร้างโฟลเดอร์ `artifacts` ก่อน export script หากยังไม่มี การส่งต่อขึ้น production สามารถนำ script ที่ EF สร้างไป review และรันด้วยกระบวนการ deployment ได้

| การเปลี่ยนแปลง | ทำอะไรในโค้ด | Migration ที่คาดหวัง |
| --- | --- | --- |
| เพิ่ม Column | เพิ่ม property และกำหนด mapping; เพิ่ม ViewModel/View ถ้าต้องรับจาก form | `AddColumn` |
| ลบ Column | ลบ property/mapping และการใช้งานที่เกี่ยวข้อง | `DropColumn` (ข้อมูลในคอลัมน์จะถูกลบ) |
| เปลี่ยน Column Type | แก้ annotation และ Fluent API ให้ตรงกัน เช่น Salary เป็น `decimal(14,2)` พร้อมแก้ validation และ HTML max | `AlterColumn` |
| เพิ่ม Index | เพิ่ม `HasIndex(...)` และกำหนดชื่อ | `CreateIndex` |
| เพิ่ม Table | เพิ่ม Entity, DbSet และ mapping/relationships | `CreateTable` |

ตัวอย่างเพิ่มคอลัมน์ nullable:

```csharp
// Models/Employee.cs
[StringLength(30)]
public string? PhoneNumber { get; set; }

// ApplicationDbContext.OnModelCreating
employee.Property(e => e.PhoneNumber)
    .HasColumnType("varchar(30)").HasMaxLength(30).IsRequired(false);
```

ใช้ nullable หรือ backfill สำหรับคอลัมน์ใหม่ที่ตารางมีข้อมูลอยู่แล้ว; หากต้องการบังคับ NOT NULL ให้เติมข้อมูลก่อนแล้วสร้าง migration อีกครั้ง การ rename คอลัมน์ควรตรวจให้เป็น `RenameColumn` แทน Drop/Add เพื่อรักษาข้อมูล

คำสั่งดูและย้อน migration:

```powershell
dotnet ef migrations list
dotnet ef migrations has-pending-model-changes
dotnet ef database update InitialCreate # ย้อนกลับก่อน migration ใหม่; ตรวจ Down ก่อน
dotnet ef migrations remove            # ใช้เมื่อ migration ล่าสุดยังไม่ได้ใช้ หรือย้อนกลับแล้ว
```

อย่าแก้ migration ที่ถูกใช้ในฐานข้อมูลร่วมกันแล้ว ให้เพิ่ม migration ใหม่แทน ตัวอย่างคำสั่ง `dotnet ef migrations add InitialCreate` ใช้เฉพาะตอนเริ่มต้นโครงการที่ยังไม่มี migration; ไม่ต้องรันซ้ำกับ repository นี้

## Jenkins deploy อัตโนมัติไป IIS

`Jenkinsfile` ทำงานตามลำดับ Checkout → Check tools → Restore → Build (Release) → Test → Publish artifact → Deploy test to IIS เฉพาะ branch `test` เท่านั้นที่ deploy; branch อื่นและ Pull Request ทำถึงขั้น Publish โดยไม่เปลี่ยนเว็บ

เครื่องนี้ใช้ Jenkins ที่ `http://localhost:8081/`, job `test-deploy`, agent label `windows-iis` และเว็บ IIS ที่ `http://myapp.test/` พอร์ต 8080 เป็นของ Apache การติดตั้ง Jenkins แบบ local นี้ต้องเปิด `Start Jenkins.cmd` ในโฟลเดอร์ `%LOCALAPPDATA%\Jenkins` และเปิด agent ด้วยสิทธิ์ Administrator ให้เชื่อมต่ออยู่จึง deploy ได้

### เตรียมเครื่อง IIS ครั้งแรก

1. ติดตั้ง Git, .NET 10 SDK ตาม `global.json`, Node.js, IIS Management Scripts and Tools และ .NET 10 Hosting Bundle บนเครื่อง IIS
2. ใน IIS Manager สร้างหรือเลือก Site ที่ Physical Path เป็น `C:\inetpub\wwwroot\test-deploy` โดยตรง ใช้ App Pool แยกสำหรับเว็บนี้ ตั้ง **.NET CLR Version = No Managed Code** และเปิด Site/App Pool ให้ทำงาน สคริปต์ค้นหาชื่อ Site/App Pool จาก Physical Path อัตโนมัติ และจะหยุดหาก App Pool ใช้ร่วมกับเว็บหรือ application อื่น
3. ตั้ง connection string สำหรับฐานข้อมูลของเซิร์ฟเวอร์ใน `appsettings.Production.json` ที่โฟลเดอร์เว็บ หรือ environment variables ของ App Pool และเตรียมฐานข้อมูล/migrations ให้พร้อม สคริปต์เก็บ `appsettings*.json` ที่มีอยู่บนเซิร์ฟเวอร์ไว้; ถ้ายังไม่มีจะคัดลอกค่าเริ่มต้นจาก publish จึงต้องกำหนดค่าฐานข้อมูลจริงก่อนใช้งาน ไม่ได้ใช้ `appsettings.Local.json` ของเครื่องพัฒนา และ pipeline ไม่รัน migration อัตโนมัติ
4. เพิ่ม Jenkins Windows agent บนเครื่อง IIS และกำหนด label **`windows-iis`** ใช้บัญชี agent ที่มีสิทธิ์จัดการ IIS และอ่าน/เขียนโฟลเดอร์เว็บกับ `C:\inetpub\deploy-backups\test-deploy` บัญชี IIS App Pool ต้องอ่านไฟล์เว็บได้ หากทดลองรัน agent ใน terminal ให้เปิด terminal แบบ **Run as administrator**

### สร้าง Jenkins job

1. New Item → **Pipeline** → Pipeline → Definition **Pipeline script from SCM** → SCM **Git**
2. Repository URL: `https://github.com/Thanarat00/test-deploy.git` เลือก credentials ที่อ่าน repository ได้หากจำเป็น
3. Branch Specifier: **`*/test`** และ Script Path: **`Jenkinsfile`**
4. Save แล้วกด **Build Now หนึ่งครั้ง** เพื่อให้ Jenkins โหลด pipeline และลงทะเบียน trigger หากยังไม่มี branch `test` ให้นำไฟล์ pipeline นี้เข้า branch ดังกล่าวก่อน
5. หลังรันครั้งแรก หน้า Build with Parameters จะมี `IIS_APP_POOL` (เว้นว่างเพื่อค้นหาอัตโนมัติ หรือใส่ชื่อที่ตรงกับ Site) และ `IIS_HEALTH_URL` (ค่าเริ่มต้น `http://myapp.test/Employees` ต้องตอบ HTTP 200 โดยเปลี่ยนให้ตรง binding จริงเมื่อใช้เครื่องอื่น) pipeline จะตรวจเว็บหลัง deploy เสมอและย้อนกลับเมื่อไม่ผ่าน ส่วนสคริปต์ PowerShell ที่เรียกใช้นอก Jenkins ยังเลือกไม่ส่ง URL ได้

ใช้ job เดียวสำหรับ deploy ปลายทางนี้ และให้ Jenkins workspace อยู่คนละโฟลเดอร์กับ IIS Physical Path ไม่ต้อง clone/pull source ลงโฟลเดอร์ที่เปิดเป็นเว็บ

### Push แล้ว deploy

pipeline ตั้ง `pollSCM('* * * * *')` ไว้แล้ว Jenkins ตรวจ Git ทุกนาทีและเริ่ม build เมื่อมี commit ใหม่ จึงทำงานได้แม้ Jenkins อยู่ใน LAN โดยไม่เปิดรับ webhook จากอินเทอร์เน็ต หลังตั้งค่าครั้งแรกแล้วใช้:

```powershell
git push origin test
```

หากต้องการเริ่มทันทีเมื่อ push ให้ติดตั้ง Jenkins GitHub plugin แล้วเพิ่ม `githubPush()` ในบล็อก `triggers` ของ Jenkinsfile คู่กับ `pollSCM` จากนั้นเพิ่ม GitHub repository → Settings → Webhooks → Push events โดยใช้ Payload URL `https://YOUR-JENKINS/github-webhook/`, Content type `application/json` GitHub ต้องเรียก URL นี้ได้ การ polling เดิมใช้เป็น fallback ได้

### พฤติกรรมตอน deploy

- Build/test/publish ใน workspace และเก็บ `artifacts/publish/` เป็น Jenkins artifacts ก่อนแตะเว็บ Test ใช้ `dotnet run` กับ `EmployeeManagement.Checks` และ `node --check`; ไม่ต้องใช้ MySQL สำหรับ CI และยังไม่รัน MySQL integration checks
- สคริปต์ `scripts/deploy-iis.ps1` ใส่ `app_offline.htm` → หยุดเฉพาะ App Pool ของเว็บ → สำรองเวอร์ชันเดิม → mirror ไฟล์ publish → นำหน้า maintenance ออก → เปิด App Pool → ตรวจ URL หากระบุ เว็บหยุดให้บริการช่วงสั้น ๆ ระหว่างการอัปเดต
- เมื่อ copy, start หรือ health check ล้มเหลว จะคืนไฟล์สำรองและเปิด App Pool กลับ พร้อมรายงาน build ว่าล้มเหลว หาก rollback ล้มเหลวจะคงหน้า maintenance ไว้และแจ้งที่อยู่ backup สำหรับกู้คืนด้วยมือ
- เก็บ `appsettings*.json`, `.git`, `logs`, `uploads` และ `App_Data` ที่มีอยู่บนเซิร์ฟเวอร์ โดย mirror เฉพาะไฟล์แอป ลบไฟล์แอปเก่าที่ไม่มีใน publish ใหม่ สำรองอยู่นอก web root ที่ `C:\inetpub\deploy-backups\test-deploy\<เวลา>-<รหัส>` และยังไม่ลบ backup อัตโนมัติ
- ป้องกัน deployment บนเครื่องเดียวกันรันซ้อนด้วย mutex จำกัด pipeline 20 นาที และเก็บประวัติ 20 builds พร้อม artifacts ของ 5 builds ล่าสุด หากเครื่องหรือ agent ถูกปิดระหว่าง deploy ให้ตรวจ `app_offline.htm`, สถานะ App Pool และ backup ก่อนรันใหม่

ทดสอบสคริปต์ด้วยข้อมูลจำลองใน `.local` โดยไม่แตะ IIS จริง:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/test-deploy-iis.ps1
```

อ้างอิง: [Jenkins Pipeline syntax](https://www.jenkins.io/doc/book/pipeline/syntax/), [GitHub webhook plugin](https://plugins.jenkins.io/github/), [ASP.NET Core on IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)

## Build และตรวจสอบ

หาก build แจ้ง `MSB3027`/`MSB3021` ว่า EmployeeManagement.exe ถูก process ใช้งานอยู่ ให้กด **Ctrl+C ใน terminal ที่รันเว็บเดิม** ก่อน build หรือใช้ start-local.ps1 ซึ่งจะหยุดเฉพาะ EmployeeManagement ที่อยู่ในโฟลเดอร์ output ของโปรเจกต์นี้ก่อน build ถ้าสิทธิ์ไม่พอ สคริปต์จะแจ้งให้ปิดเว็บเดิมด้วย Ctrl+C

อ้างอิง MySqlConnector `2.6.1` โดยตรงเพื่อไม่ให้ Pomelo เลือก dependency ขั้นต่ำ `2.4.0` ซึ่งมีคำเตือน NU1902; รุ่นที่แก้ไขระบุใน [release notes 2.6.1](https://github.com/mysql-net/MySqlConnector/releases/tag/2.6.1)

```powershell
# จาก repository root
dotnet build EmployeeManagement.sln --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/test.ps1
```

ชุด `EmployeeManagement.Checks` compile Razor views และโค้ด Model/ViewModel/Controller ตัวจริงโดย link source จึงตรวจได้โดยไม่ต้องมี MySQL หรือ provider package แต่ **ไม่ได้ยืนยัน EF/Pomelo หรือ build แอปหลัก** ตรวจ validation, เงินเดือน, pagination, MVC results และ POST/anti-forgery attributes

ทดสอบ integration ด้วย MySQL จริง:

```powershell
$env:EMPLOYEE_TEST_MYSQL = 'Server=localhost;Port=3306;User=YOUR_TEST_USER;Password=YOUR_TEST_PASSWORD;DateTimeKind=Utc'
# สำหรับ MariaDB ตั้งสองค่านี้ด้วย (MySQL ใช้ค่าเริ่มต้นได้)
# $env:EMPLOYEE_TEST_PROVIDER = 'MariaDb'
# $env:EMPLOYEE_TEST_SERVER_VERSION = '10.4.32'
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/test.ps1 -MySql
```

บัญชีทดสอบต้องสร้างและลบฐานข้อมูลได้ ชุดทดสอบสร้างฐานข้อมูล `EmployeeManagementTests_<GUID>` ใหม่ ใช้ `MigrateAsync` แล้วลบเฉพาะฐานข้อมูลนั้นใน finally ไม่ใช้ฐานข้อมูลใน appsettings ทดสอบ snapshot, migration history, CRUD, Unique Index, ภาษาไทย, timestamps, Inactive, decimal, search, sort และ pagination

## สถานะการตรวจสอบใน workspace นี้

ผ่าน **33 checks** รวม MVC/validation, Razor compilation, database engine/version configuration และ JavaScript syntax ตรวจเชื่อมต่อ MariaDB 10.4.32 จริงได้ และสคริปต์เตรียมฐานข้อมูล/บัญชีแอปสำเร็จ รวมการรันซ้ำโดยใช้บัญชีเดิม

ตรวจ Apache ของ XAMPP ที่ `http://127.0.0.1:8080/` ตอบ HTTP 200 แล้ว ซึ่งเป็นคนละ process/port กับเว็บ EmployeeManagement ที่จะรันบน 5034

การตรวจล่าสุดรัน `scripts/test.ps1` ผ่าน 33 checks และ JavaScript syntax พร้อม `dotnet publish --configuration Release --no-restore` สำเร็จโดยใช้ packages ที่มีอยู่แล้ว สคริปต์ deploy ผ่าน 75 checks โดยใช้ Robocopy จริงกับโฟลเดอร์จำลองและคำสั่ง IIS จำลอง ครอบคลุมอัปเดตสำเร็จ, copy/start/health check ล้มเหลว, rollback, การเก็บ server settings และการปฏิเสธ App Pool ที่ใช้ร่วมกัน Jenkins ตรวจ syntax ของ Jenkinsfile ผ่านแล้ว และ job เตรียมค่า Production บน agent จริงสำเร็จพร้อมเว็บตอบ HTTP 200 ดูผล deployment ล่าสุดใน job `test-deploy` ของ Jenkins รอบนี้ไม่ได้รัน EF migrations หรือ MySQL integration tests

แอปนี้ไม่มีระบบ login หรือกำหนดสิทธิ์ผู้ใช้ตามขอบเขต CRUD ที่ระบุ
