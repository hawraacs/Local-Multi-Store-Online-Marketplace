using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Core.Interfaces;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories;
using Multi_Store.Services;
using Multi_Store.Services.Managers;
using AutoMapper;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Razor Pages + Role Protection
// =========================
builder.Services.AddRazorPages(options =>
{
    // Admin Pages
    options.Conventions.AuthorizePage("/Admin1", "AdminOnly");
    options.Conventions.AuthorizePage("/AdminUsers", "AdminOnly");
    options.Conventions.AuthorizePage("/AdminStores", "AdminOnly");
    options.Conventions.AuthorizePage("/AdminDelivery", "AdminOnly");

    // Customer Pages
    options.Conventions.AuthorizePage("/Customer1", "CustomerOnly");
    options.Conventions.AuthorizePage("/CustomerProducts", "CustomerOnly");
    options.Conventions.AuthorizePage("/CustomerCart", "CustomerOnly");
    options.Conventions.AuthorizePage("/CustomerOrders", "CustomerOnly");
    options.Conventions.AuthorizePage("/CustomerWishlist", "CustomerOnly");
    options.Conventions.AuthorizePage("/CustomerAddresses", "CustomerOnly");
    options.Conventions.AuthorizePage("/CustomerProfile", "CustomerOnly");
    options.Conventions.AuthorizePage("/StoreRequest", "CustomerOnly");

    // Store Owner Pages
    options.Conventions.AuthorizeFolder("/StoreOwner", "StoreOwnerOnly");

    // Delivery Pages
    options.Conventions.AuthorizePage("/Delivery1", "DeliveryOnly");
    options.Conventions.AuthorizePage("/DeliveryOrders", "DeliveryOnly");
    options.Conventions.AuthorizePage("/DeliveryEarnings", "DeliveryOnly");
    options.Conventions.AuthorizePage("/DeliveryProfile", "DeliveryOnly");
});

// =========================
// Database
// =========================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// =========================
// Identity
// =========================
builder.Services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// =========================
// Authorization Policies
// =========================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("CustomerOnly", policy =>
        policy.RequireRole("Customer"));

    options.AddPolicy("StoreOwnerOnly", policy =>
        policy.RequireRole("StoreOwner"));

    options.AddPolicy("DeliveryOnly", policy =>
        policy.RequireRole("Delivery"));
});

// =========================
// Session / Cookie Timeout
// =========================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    // Auto logout after 30 minutes inactive
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// =========================
// Google + Facebook Authentication
// =========================
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddGoogle(options =>
    {
        options.ClientId =
            builder.Configuration["Authentication:Google:ClientId"]!;

        options.ClientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"]!;
    })
    .AddFacebook(options =>
    {
        options.AppId =
            builder.Configuration["Authentication:Facebook:AppId"]!;

        options.AppSecret =
            builder.Configuration["Authentication:Facebook:AppSecret"]!;
    });

// =========================
// Repositories
// =========================
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
builder.Services.AddScoped<IRecentlyViewedProductRepository, RecentlyViewedProductRepository>();

// Current Store Service
builder.Services.AddScoped<ICurrentStoreService, CurrentStoreService>();

// AutoMapper
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

// =========================
// Managers
// =========================
builder.Services.AddScoped<Multi_Store.Services.Managers.UserManager>();
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
builder.Services.AddScoped<WishlistManager>();
builder.Services.AddScoped<CustomerAddressManager>();
builder.Services.AddScoped<OrderHistoryManager>();
builder.Services.AddScoped<RecentlyViewedManager>();

var app = builder.Build();

// =========================
// Seed Roles
// =========================
using (var scope = app.Services.CreateScope())
{
    var roleManager =
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

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
            await roleManager.CreateAsync(
                new IdentityRole<int>(role));
        }
    }
}

// =========================
// Seed Initial Data
// =========================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}

// =========================
// Middleware
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapRazorPages();

app.MapDefaultControllerRoute();

app.Run();