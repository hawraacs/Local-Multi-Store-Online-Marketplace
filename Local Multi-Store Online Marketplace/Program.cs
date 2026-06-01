using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Core.Interfaces;  // ✅ ADD THIS - for ICurrentStoreService
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories;
using Multi_Store.Services.Managers;
using Multi_Store.Services;  // ✅ ADD THIS - for CurrentStoreService
using AutoMapper;
<<<<<<< HEAD
=======
using Multi_Store.Services;
>>>>>>> c04741d09f85a7339dc44fc85ca1280b540f5f5b

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages support
builder.Services.AddRazorPages();

// Register your ApplicationDbContext (your custom database context)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

<<<<<<< HEAD
// Register HttpContextAccessor (needed for CurrentStoreService)
builder.Services.AddHttpContextAccessor();  // ✅ ADD THIS

// Register all repositories
=======
// Google + Facebook Authentication
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"];
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
    });

>>>>>>> c04741d09f85a7339dc44fc85ca1280b540f5f5b
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<ICartItemRepository, CartItemRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IComplaintRepository, ComplaintRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
builder.Services.AddScoped<IDeliveryAreaRepository, DeliveryAreaRepository>();
builder.Services.AddScoped<IDeliveryAssignmentRepository, DeliveryAssignmentRepository>();
builder.Services.AddScoped<IDeliveryPersonRepository, DeliveryPersonRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<IOrderStatusHistoryRepository, OrderStatusHistoryRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
builder.Services.AddScoped<IRefundRequestRepository, RefundRequestRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();

<<<<<<< HEAD
// ✅ ADD THIS - Register CurrentStoreService
builder.Services.AddScoped<ICurrentStoreService, CurrentStoreService>();

// Register AutoMapper
=======
>>>>>>> c04741d09f85a7339dc44fc85ca1280b540f5f5b
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

// Register all managers
builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<StoreManager>();
builder.Services.AddScoped<ProductManager>();
builder.Services.AddScoped<CategoryManager>();
builder.Services.AddScoped<CartManager>();
builder.Services.AddScoped<OrderManager>();
builder.Services.AddScoped<PaymentManager>();
builder.Services.AddScoped<DeliveryManager>();
builder.Services.AddScoped<ReviewManager>();
builder.Services.AddScoped<NotificationManager>();
builder.Services.AddScoped<ComplaintManager>();
builder.Services.AddScoped<MessagingManager>();

var app = builder.Build();

<<<<<<< HEAD
// Seed roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
=======
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}

using (var scope = app.Services.CreateScope())
{
    var roleManager =
        scope.ServiceProvider
>>>>>>> c04741d09f85a7339dc44fc85ca1280b540f5f5b
        .GetRequiredService<RoleManager<IdentityRole<int>>>();

    string[] roles =
    {
        "Admin",
        "StoreOwner",
        "Customer",
        "Delivery"
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(role));
        }
    }
}

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();  // HTTP Strict Transport Security
}

app.UseHttpsRedirection();   // Redirect HTTP to HTTPS
<<<<<<< HEAD
app.UseStaticFiles();         // Serve static files (CSS, JS, Images)
app.UseRouting();             // Enable routing
app.UseAuthentication();      // ✅ ADD THIS - Enable authentication
app.UseAuthorization();       // Enable authorization (roles)
app.MapRazorPages();          // Map Razor Pages endpoints

app.Run();  // Run the application
=======
app.UseStaticFiles();        // Serve static files (CSS, JS, Images)

app.UseRouting();            // Enable routing

app.UseAuthentication();     // Enable authentication (login, Google, Facebook)
app.UseAuthorization();      // Enable authorization (roles)

app.MapRazorPages();         // Map Razor Pages endpoints
app.MapDefaultControllerRoute();

app.Run();                   // Run the application
>>>>>>> c04741d09f85a7339dc44fc85ca1280b540f5f5b
