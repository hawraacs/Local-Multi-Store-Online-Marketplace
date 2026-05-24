using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Local_Multi_Store_Online_Marketplace.Core.Entities;

namespace Local_Multi_Store_Online_Marketplace.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // =========================
    // USERS & ROLES SYSTEM
    // =========================

    public DbSet<Role> Roles { get; set; }

    public DbSet<User> Users { get; set; }

    // =========================
    // CUSTOMER MODULE
    // =========================

    public DbSet<Customer> Customers { get; set; }

    public DbSet<CustomerAddress> CustomerAddresses { get; set; }

    // =========================
    // STORE MODULE
    // =========================

    public DbSet<Store> Stores { get; set; }

    public DbSet<DeliveryArea> DeliveryAreas { get; set; }

    // =========================
    // PRODUCT MODULE
    // =========================

    public DbSet<Category> Categories { get; set; }

    public DbSet<Product> Products { get; set; }

    public DbSet<ProductImage> ProductImages { get; set; }

    // =========================
    // CART MODULE
    // =========================

    public DbSet<Cart> Carts { get; set; }

    // =========================
    // ORDER MODULE
    // =========================

    public DbSet<Order> Orders { get; set; }

    public DbSet<OrderItem> OrderItems { get; set; }

    // =========================
    // PAYMENT MODULE
    // =========================

    public DbSet<Payment> Payments { get; set; }

    public DbSet<RefundRequest> RefundRequests { get; set; }

    // =========================
    // COMMUNICATION
    // =========================

    public DbSet<ChatMessage> ChatMessages { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    // =========================
    // REVIEWS & COMPLAINTS
    // =========================

    public DbSet<Review> Reviews { get; set; }

    public DbSet<Complaint> Complaints { get; set; }

    // =========================
    // DELIVERY MODULE
    // =========================

    public DbSet<DeliveryPerson> DeliveryPersons { get; set; }

    public DbSet<DeliveryAssignment> DeliveryAssignments { get; set; }

    // OPTIONAL (if you later extend)
    // public DbSet<Wishlist> Wishlists { get; set; }
    // public DbSet<CartItem> CartItems { get; set; }
}