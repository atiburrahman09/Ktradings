using KTrading.Data;
using KTrading.Repositories;
using KTrading.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=.;Database=DistributionSales;Trusted_Connection=True;MultipleActiveResultSets=true";

// Add services
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.UseCompatibilityLevel(120)));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Require authentication for all Razor Pages by default, allow anonymous for login/register
builder.Services.AddRazorPages(options =>
{
    // Protect entire site
    options.Conventions.AuthorizeFolder("/");

    // Allow anonymous to login/register pages we provide
    options.Conventions.AllowAnonymousToPage("/Account/Login");

    // Keep unauthenticated users on the custom login page.
});

builder.Services.AddControllersWithViews();

// DI: repositories and services
builder.Services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminOrSales", policy => policy.RequireRole("Admin", "Sales"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

// Ensure database exists before handling requests
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        // If migrations exist, Apply them; otherwise create database schema based on model
        try
        {
            db.Database.Migrate();
        }
        catch
        {
            db.Database.EnsureCreated();
        }

        // Seed initial data
        try
        {
            await SeedData.InitializeAsync(services);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
