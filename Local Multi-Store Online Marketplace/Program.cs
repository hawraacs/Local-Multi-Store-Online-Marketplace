using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Interfaces;
using Multi_Store.Core.Managers;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Infrastructure.Repositories;
using Multi_Store.Services;
using Multi_Store.Services.Email;
using Multi_Store.Services.Managers;
using Multi_Store.Infrastructure.Settings;
using Multi_Store.Infrastructure.Services;
using Multi_Store.Services.Managers;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// Razor Pages + Controllers
// ===============================
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// ===============================
// HttpContextAccessor
// ===============================
builder.Services.AddHttpContextAccessor();

// ===============================
// Database
// ===============================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ===============================
// Email Settings
// ===============================
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
//twiolo 
builder.Services.Configure<TwilioSettings>(
    builder.Configuration.GetSection("Twilio"));

// ===============================
// Identity
// ===============================
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    options.SignIn.RequireConfirmedEmail = false;
    options.User.RequireUniqueEmail = true;

    options.Lockout.MaxFailedAccessAttempts = 10;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(30);
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ===============================
// FR-06 Session / Cookie Timeout
// ===============================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ===============================
// Google + Facebook Authentication
// ===============================
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddGoogle(options =>
    {
        options.ClientId =
            builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;

        options.ClientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
    })
    .AddFacebook(options =>
    {
        options.AppId =
            builder.Configuration["Authentication:Facebook:AppId"] ?? string.Empty;

        options.AppSecret =
            builder.Configuration["Authentication:Facebook:AppSecret"] ?? string.Empty;
    });




// ===============================
// Repositories
// ===============================
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
builder.Services.AddScoped<IDeliveryPaymentCollectionRepository, DeliveryPaymentCollectionRepository>();
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
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<IStoryViewRepository, StoryViewRepository>();
builder.Services.AddScoped<IStoryLikeRepository, StoryLikeRepository>();
builder.Services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IRecentlyViewedProductRepository, RecentlyViewedProductRepository>();
builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();

// ===============================
// Services
// ===============================
builder.Services.AddScoped<ITwilioService, TwilioService>();
builder.Services.AddScoped<ICurrentStoreService, CurrentStoreService>();
builder.Services.AddScoped<SubscriptionService>();

// ===============================
// AutoMapper
// ===============================
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

// ===============================
// Managers
// ===============================
builder.Services.AddScoped<Multi_Store.Services.Managers.UserManager>();
builder.Services.AddScoped<StoreManager>();
builder.Services.AddScoped<StoryManager>();
builder.Services.AddScoped<ProductManager>();
builder.Services.AddScoped<CategoryManager>();
builder.Services.AddScoped<CartManager>();
builder.Services.AddScoped<OrderManager>();
builder.Services.AddScoped<PaymentManager>();
builder.Services.AddScoped<DeliveryManager>();
builder.Services.AddScoped<ReviewManager>();
builder.Services.AddScoped<NotificationManager>();
builder.Services.AddScoped<BoostManager>();
builder.Services.AddScoped<ComplaintManager>();
builder.Services.AddScoped<MessagingManager>();
builder.Services.AddScoped<WishlistManager>();
builder.Services.AddScoped<CustomerAddressManager>();
builder.Services.AddScoped<OrderHistoryManager>();
builder.Services.AddScoped<RecentlyViewedManager>();
builder.Services.AddScoped<CustomerManager>();
builder.Services.AddScoped<SessionManager>();

builder.Services.AddScoped<IPromotionManager, PromotionManager>();

builder.Services.AddScoped<OtpManager>();
builder.Services.AddScoped<EmailotppManager>();
builder.Services.AddScoped<SmsOtpManager>();
builder.Services.AddDistributedMemoryCache(); // in-memory session store
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

// ===============================
// Seed Roles
// ===============================
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
            await roleManager.CreateAsync(new IdentityRole<int>(role));
        }
    }
}

// ===============================
// Seed Initial Data
// ===============================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}

// ===============================
// Debug Database Info
// ===============================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    Console.WriteLine("DB CONNECTION:");
    Console.WriteLine(db.Database.GetConnectionString());

    Console.WriteLine("DB NAME:");
    Console.WriteLine(db.Database.GetDbConnection().Database);
}

// ===============================
// Middleware
// ===============================
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

app.UseSession();

app.MapRazorPages();
app.MapControllers();
app.MapDefaultControllerRoute();

app.Run();