using Microsoft.EntityFrameworkCore;

using Multi_Store.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// 1. ADD SERVICES TO THE CONTAINER
// =============================================

// Add Razor Pages support
builder.Services.AddRazorPages();

// Register your ApplicationDbContext (your custom database context)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// =============================================
// 2. SEED THE DATABASE (IMPORTANT!)
// =============================================
// This creates Roles (Admin, Customer, StoreOwner, DeliveryStaff)
// and creates the default Admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}


// =============================================
// 3. CONFIGURE HTTP PIPELINE
// =============================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();  // HTTP Strict Transport Security
}

app.UseHttpsRedirection();   // Redirect HTTP to HTTPS
app.UseStaticFiles();         // Serve static files (CSS, JS, Images)
app.UseRouting();             // Enable routing
app.UseAuthorization();       // Enable authorization (roles)
app.MapRazorPages();          // Map Razor Pages endpoints

app.Run();  // Run the application