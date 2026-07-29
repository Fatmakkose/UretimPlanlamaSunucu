using Microsoft.EntityFrameworkCore;
using UretimPlanlama.Data;
using Microsoft.AspNetCore.Identity;
using UretimPlanlama.Models;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5101");

builder.Host.UseWindowsService();

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=localhost;Initial Catalog=UretimPlanlamaDb;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Command Timeout=0";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.Configure<UretimPlanlama.Models.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<UretimPlanlama.Services.IEmailService, UretimPlanlama.Services.EmailService>();
builder.Services.AddHostedService<UretimPlanlama.Services.DailySummaryHostedService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlanlamaAccess", policy => policy.RequireAssertion(context => context.User.IsInRole("Admin") || context.User.HasClaim("Permission", "Page_Planlama")));
    options.AddPolicy("DepoAccess", policy => policy.RequireAssertion(context => context.User.IsInRole("Admin") || context.User.HasClaim("Permission", "Page_Depo")));
    options.AddPolicy("SurecAccess", policy => policy.RequireAssertion(context => context.User.IsInRole("Admin") || context.User.HasClaim("Permission", "Page_Surec")));
    options.AddPolicy("SiparisAccess", policy => policy.RequireAssertion(context => context.User.IsInRole("Admin") || context.User.HasClaim("Permission", "Page_Siparis")));
    options.AddPolicy("RaporAccess", policy => policy.RequireAssertion(context => context.User.IsInRole("Admin") || context.User.HasClaim("Permission", "Page_Rapor")));
});

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Varsayılan HSTS değeri 30 gündür. Üretim senaryoları için bunu değiştirmek isteyebilirsiniz, bkz. https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Ofis ağında HTTP kullanılacak
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<UretimPlanlama.Hubs.NotificationHub>("/notificationHub");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        connection.Open();
        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName)) continue;

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                var mapping = property.FindRelationalTypeMapping();
                if (mapping == null) continue;
                var columnType = mapping.StoreType;

                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[{tableName}]') AND name = N'{columnName}'";
                var count = (int)checkCmd.ExecuteScalar();

                if (count == 0)
                {
                    string defaultConstraint = property.IsNullable ? "NULL" : (columnType.Contains("bit") ? "NOT NULL DEFAULT 0" : (columnType.Contains("int") || columnType.Contains("decimal") || columnType.Contains("float") || columnType.Contains("datetime") ? "NOT NULL DEFAULT 0" : "NOT NULL DEFAULT ''"));
                    
                    // Handle datetime default
                    if (defaultConstraint == "NOT NULL DEFAULT 0" && columnType.Contains("datetime")) 
                    {
                        defaultConstraint = "NOT NULL DEFAULT GETDATE()";
                    }

                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnType} {defaultConstraint}";
                    alterCmd.ExecuteNonQuery();
                    Console.WriteLine($"Auto-Migration: Added missing column [{columnName}] to table [{tableName}] with type [{columnType}]");
                }
            }
        }
        connection.Close();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Database column auto-migration warning: " + ex.Message);
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    if (!roleManager.RoleExistsAsync("Admin").Result)
    {
        roleManager.CreateAsync(new IdentityRole("Admin")).Wait();
    }
    if (!roleManager.RoleExistsAsync("User").Result)
    {
        roleManager.CreateAsync(new IdentityRole("User")).Wait();
    }

    var userRole = roleManager.FindByNameAsync("User").Result;
    if (userRole != null)
    {
        var roleClaims = roleManager.GetClaimsAsync(userRole).Result;
        if (!roleClaims.Any(c => c.Type == "Permission" && c.Value == "View"))
        {
            roleManager.AddClaimAsync(userRole, new System.Security.Claims.Claim("Permission", "View")).Wait();
        }
        if (!roleClaims.Any(c => c.Type == "Permission" && c.Value == "Write"))
        {
            roleManager.AddClaimAsync(userRole, new System.Security.Claims.Claim("Permission", "Write")).Wait();
        }
    }

    if (userManager.FindByEmailAsync("fatma@cps.com").Result == null)
    {
        var user = new ApplicationUser
        {
            UserName = "fatma@cps.com",
            Email = "fatma@cps.com",
            FullName = "Fatma",
            RoleTitle = "Production Planner"
        };
        var result = userManager.CreateAsync(user, "Fatma123!").Result;
        if (result.Succeeded)
        {
            userManager.AddToRoleAsync(user, "Admin").Wait();
        }
    }
}

app.Run();
