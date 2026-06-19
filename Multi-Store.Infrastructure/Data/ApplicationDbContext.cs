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

        public ApplicationDbContext()
        {
        }

        // ================= DbSets =================
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
        public DbSet<StoreFollow> StoreFollows { get; set; }
        public DbSet<RecentlyViewedProduct> RecentlyViewedProducts { get; set; }
        public DbSet<PasswordResetOtp> PasswordResetOtps { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<PromotionRecipient> PromotionRecipients { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ================= KEYS =================
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

            // ================= INDEXES =================
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Store>().HasIndex(s => s.StoreCode).IsUnique();
            modelBuilder.Entity<Store>().HasIndex(s => s.OwnerUserID).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.CategorySlug).IsUnique();
            modelBuilder.Entity<Coupon>().HasIndex(c => c.CouponCode).IsUnique();
            modelBuilder.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();
            modelBuilder.Entity<SystemConfig>().HasIndex(sc => sc.ConfigKey).IsUnique();
            modelBuilder.Entity<DeliveryPerson>().HasIndex(d => d.RequestedByUserID);

            // ================= RELATIONSHIPS =================
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithOne(u => u.Customer)
                .HasForeignKey<Customer>(c => c.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Store>()
                .HasOne(s => s.Owner)
                .WithOne(u => u.Store)
                .HasForeignKey<Store>(s => s.OwnerUserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeliveryPerson>()
                .HasOne(d => d.User)
                .WithOne(u => u.DeliveryPerson)
                .HasForeignKey<DeliveryPerson>(d => d.UserID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DeliveryPerson>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(d => d.RequestedByUserID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.DefaultAddress)
                .WithMany()
                .HasForeignKey(c => c.DefaultAddressID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.OrderItem)
                .WithOne(oi => oi.Review)
                .HasForeignKey<Review>(r => r.OrderItemID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerAddress>()
                .HasOne(ca => ca.Customer)
                .WithMany(c => c.Addresses)
                .HasForeignKey(ca => ca.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.Customer)
                .WithMany(c => c.Wishlists)
                .HasForeignKey(w => w.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Customer)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Complaint>()
                .HasOne(c => c.Customer)
                .WithMany(cu => cu.Complaints)
                .HasForeignKey(c => c.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Store)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.StoreID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CartID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(ci => ci.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefundRequest>()
                .HasOne(r => r.Order)
                .WithOne(o => o.RefundRequest)
                .HasForeignKey<RefundRequest>(r => r.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeliveryAssignment>()
                .HasOne(d => d.Order)
                .WithOne(o => o.DeliveryAssignment)
                .HasForeignKey<DeliveryAssignment>(d => d.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeliveryAssignment>()
                .HasOne(d => d.DeliveryPerson)
                .WithMany(dp => dp.Assignments)
                .HasForeignKey(d => d.DeliveryPersonID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(cm => cm.SenderID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(cm => cm.ReceiverID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PasswordResetOtp>(entity =>
            {
                entity.HasKey(x => x.PasswordResetOtpID);

                entity.Property(x => x.DeliveryMethod)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(x => x.Target)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.Property(x => x.OtpHash)
                    .IsRequired();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Promotion>()
    .HasKey(p => p.PromotionID);

            modelBuilder.Entity<Promotion>()
                .HasOne(p => p.Store)
                .WithMany()
                .HasForeignKey(p => p.StoreID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PromotionRecipient>()
                .HasKey(pr => pr.PromotionRecipientID);

            modelBuilder.Entity<PromotionRecipient>()
                .HasOne(pr => pr.Promotion)
                .WithMany(p => p.PromotionRecipients)
                .HasForeignKey(pr => pr.PromotionID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PromotionRecipient>()
                .HasOne(pr => pr.Customer)
                .WithMany()
                .HasForeignKey(pr => pr.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);
            // ================= RECENTLY VIEWED PRODUCTS =================
            modelBuilder.Entity<RecentlyViewedProduct>(entity =>
            {
                entity.ToTable("RecentlyViewedProducts");

                entity.HasKey(rv => rv.Id);

                entity.Property(rv => rv.Id)
                    .HasColumnName("ID");

                entity.Property(rv => rv.CustomerID)
                    .HasColumnName("CustomerId");

                entity.Property(rv => rv.ProductID)
                    .HasColumnName("ProductID");

                entity.Property(rv => rv.ViewedAt)
                    .HasColumnName("ViewedAt");

                entity.HasIndex(rv => new { rv.CustomerID, rv.ProductID })
                    .IsUnique();
            });

            // ================= CONFIG =================
            modelBuilder.Entity<Product>().Ignore(p => p.IsOutOfStock);

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderDate)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Store>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Cart>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Cart>()
                .Property(c => c.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Review>()
                .ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "Rating >= 1 AND Rating <= 5"));

            // ================= PRECISION =================
            modelBuilder.Entity<Store>().Property(s => s.CommissionRate).HasPrecision(18, 2);
            modelBuilder.Entity<Store>().Property(s => s.Rating).HasPrecision(18, 2);
            modelBuilder.Entity<Store>().Property(s => s.CODMaxLimit).HasPrecision(18, 2);
            modelBuilder.Entity<Store>().Property(s => s.Latitude).HasPrecision(18, 6);
            modelBuilder.Entity<Store>().Property(s => s.Longitude).HasPrecision(18, 6);

            modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.CompareAtPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.Rating).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.Weight).HasPrecision(18, 2);

            modelBuilder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.DeliveryFee).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.DiscountAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.TaxAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.TotalAmount).HasPrecision(18, 2);

            modelBuilder.Entity<CartItem>().Property(ci => ci.PriceAtAddTime).HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>().Property(oi => oi.ProductPrice).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>().Property(oi => oi.TotalPrice).HasPrecision(18, 2);

            modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(p => p.RefundAmount).HasPrecision(18, 2);

            modelBuilder.Entity<RefundRequest>().Property(r => r.RequestedAmount).HasPrecision(18, 2);
            modelBuilder.Entity<RefundRequest>().Property(r => r.ApprovedAmount).HasPrecision(18, 2);
        }
    }
}