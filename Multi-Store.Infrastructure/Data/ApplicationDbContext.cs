// Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Multi_Store.Core.Entities;

namespace Multi_Store.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Default constructor for migrations
        public ApplicationDbContext()
        {
        }

        

        // =============================================
        // ALL DbSets (27 Tables)
        // =============================================
       
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<Store> Stores { get; set; }
        public DbSet<DeliveryArea> DeliveryAreas { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<RefundRequest> RefundRequests { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<DeliveryPerson> DeliveryPersons { get; set; }
        public DbSet<DeliveryAssignment> DeliveryAssignments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        public DbSet<Session> Sessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =============================================
            // EXPLICIT PRIMARY KEYS (Fix for ChatMessage and others)
            // =============================================
            modelBuilder.Entity<AuditLog>().HasKey(e => e.AuditLogID);
            modelBuilder.Entity<Cart>().HasKey(e => e.CartID);
            modelBuilder.Entity<CartItem>().HasKey(e => e.CartItemID);
            modelBuilder.Entity<Category>().HasKey(e => e.CategoryID);
            modelBuilder.Entity<ChatMessage>().HasKey(e => e.MessageID);
            modelBuilder.Entity<Complaint>().HasKey(e => e.ComplaintID);
            modelBuilder.Entity<Coupon>().HasKey(e => e.CouponID);
            modelBuilder.Entity<Customer>().HasKey(e => e.CustomerID);
            modelBuilder.Entity<CustomerAddress>().HasKey(e => e.AddressID);
            modelBuilder.Entity<DeliveryArea>().HasKey(e => e.DeliveryAreaID);
            modelBuilder.Entity<DeliveryAssignment>().HasKey(e => e.AssignmentID);
            modelBuilder.Entity<DeliveryPerson>().HasKey(e => e.DeliveryPersonID);
            modelBuilder.Entity<Notification>().HasKey(e => e.NotificationID);
            modelBuilder.Entity<Order>().HasKey(e => e.OrderID);
            modelBuilder.Entity<OrderItem>().HasKey(e => e.OrderItemID);
            modelBuilder.Entity<OrderStatusHistory>().HasKey(e => e.StatusHistoryID);
            modelBuilder.Entity<Payment>().HasKey(e => e.PaymentID);
            modelBuilder.Entity<Product>().HasKey(e => e.ProductID);
            modelBuilder.Entity<ProductImage>().HasKey(e => e.ImageID);
            modelBuilder.Entity<RefundRequest>().HasKey(e => e.RefundRequestID);
            modelBuilder.Entity<Review>().HasKey(e => e.ReviewID);
            
            modelBuilder.Entity<Session>().HasKey(e => e.SessionID);
            modelBuilder.Entity<Store>().HasKey(e => e.StoreID);
            modelBuilder.Entity<SystemConfig>().HasKey(e => e.ConfigID);
           
            modelBuilder.Entity<Wishlist>().HasKey(e => e.WishlistID);

            // =============================================
            // UNIQUE CONSTRAINTS
            // =============================================
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Store>()
                .HasIndex(s => s.StoreCode)
                .IsUnique();

            modelBuilder.Entity<Store>()
                .HasIndex(s => s.OwnerUserID)
                .IsUnique();

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategorySlug)
                .IsUnique();

            modelBuilder.Entity<Coupon>()
                .HasIndex(c => c.CouponCode)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<SystemConfig>()
                .HasIndex(sc => sc.ConfigKey)
                .IsUnique();

            // =============================================
            // ONE-TO-ONE RELATIONSHIPS
            // =============================================

            // User → Customer (Optional One-to-One)
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithOne(u => u.Customer)
                .HasForeignKey<Customer>(c => c.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            // User → Store (Optional One-to-One)
            modelBuilder.Entity<Store>()
                .HasOne(s => s.Owner)
                .WithOne(u => u.Store)
                .HasForeignKey<Store>(s => s.OwnerUserID)
                .OnDelete(DeleteBehavior.Restrict);

            // User → DeliveryPerson (Optional One-to-One)
            modelBuilder.Entity<DeliveryPerson>()
                .HasOne(d => d.User)
                .WithOne(u => u.DeliveryPerson)
                .HasForeignKey<DeliveryPerson>(d => d.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer → DefaultAddress (Optional One-to-One)
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.DefaultAddress)
                .WithMany()
                .HasForeignKey(c => c.DefaultAddressID)
                .OnDelete(DeleteBehavior.Restrict);

            // OrderItem → Review (One-to-One)
            modelBuilder.Entity<Review>()
                .HasOne(r => r.OrderItem)
                .WithOne(oi => oi.Review)
                .HasForeignKey<Review>(r => r.OrderItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // =============================================
            // ONE-TO-MANY RELATIONSHIPS
            // =============================================

            // Role → User
           
                

            // Customer → CustomerAddress
            modelBuilder.Entity<CustomerAddress>()
                .HasOne(ca => ca.Customer)
                .WithMany(c => c.Addresses)
                .HasForeignKey(ca => ca.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);

            // Customer → Wishlist
            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.Customer)
                .WithMany(c => c.Wishlists)
                .HasForeignKey(w => w.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);

            // Customer → Order
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer → Review
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Customer)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer → Complaint
            modelBuilder.Entity<Complaint>()
                .HasOne(c => c.Customer)
                .WithMany(cu => cu.Complaints)
                .HasForeignKey(c => c.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            // Store → Product
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Store)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.StoreID)
                .OnDelete(DeleteBehavior.Cascade);

            // Store → DeliveryArea
            modelBuilder.Entity<DeliveryArea>()
                .HasOne(da => da.Store)
                .WithMany(s => s.DeliveryAreas)
                .HasForeignKey(da => da.StoreID)
                .OnDelete(DeleteBehavior.Cascade);

            // Store → Coupon
            modelBuilder.Entity<Coupon>()
                .HasOne(c => c.Store)
                .WithMany(s => s.Coupons)
                .HasForeignKey(c => c.StoreID)
                .OnDelete(DeleteBehavior.Cascade);

            // Category → Product
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            // Category self-reference (Parent → SubCategories)
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            // Product → ProductImage
            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            // Cart → CartItem
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CartID)
                .OnDelete(DeleteBehavior.Cascade);

            // Product → CartItem
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(ci => ci.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → OrderItem
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            // Product → OrderItem
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → OrderStatusHistory
            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(osh => osh.Order)
                .WithMany(o => o.StatusHistory)
                .HasForeignKey(osh => osh.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            // Order → Payment
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → RefundRequest (Optional One-to-One)
            modelBuilder.Entity<RefundRequest>()
                .HasOne(rr => rr.Order)
                .WithOne(o => o.RefundRequest)
                .HasForeignKey<RefundRequest>(rr => rr.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → DeliveryAssignment
            modelBuilder.Entity<DeliveryAssignment>()
                .HasOne(da => da.Order)
                .WithOne(o => o.DeliveryAssignment)
                .HasForeignKey<DeliveryAssignment>(da => da.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            // DeliveryPerson → DeliveryAssignment
            modelBuilder.Entity<DeliveryAssignment>()
                .HasOne(da => da.DeliveryPerson)
                .WithMany(dp => dp.Assignments)
                .HasForeignKey(da => da.DeliveryPersonID)
                .OnDelete(DeleteBehavior.Restrict);

            // User → AuditLog
            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            // User → Notification
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // User → ChatMessage (Sender)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(cm => cm.SenderID)
                .OnDelete(DeleteBehavior.Restrict);

            // User → ChatMessage (Receiver)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(cm => cm.ReceiverID)
                .OnDelete(DeleteBehavior.Restrict);

            // =============================================
            // ADDITIONAL CONFIGURATIONS
            // =============================================

            // Product - Computed column for IsOutOfStock
            modelBuilder.Entity<Product>()
                .Ignore(p => p.IsOutOfStock);

            // Order - Default value for OrderDate
            modelBuilder.Entity<Order>()
                .Property(o => o.OrderDate)
                .HasDefaultValueSql("GETUTCDATE()");

            // User - Default value for CreatedAt
            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Store - Default value for CreatedAt
            modelBuilder.Entity<Store>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Cart - Default values
            modelBuilder.Entity<Cart>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Cart>()
                .Property(c => c.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Review - Rating range validation
            modelBuilder.Entity<Review>()
                .ToTable(tb => tb.HasCheckConstraint("CK_Review_Rating", "Rating >= 1 AND Rating <= 5"));
        }
    }
}