// Data/SeedData.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multi_Store.Core.Entities;
using System.Security.Cryptography;
using System.Text;

namespace Multi_Store.Infrastructure.Data
{
    public static class SeedData
    {
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // =============================================
            // 1. SEED ROLES
            // =============================================
            if (!context.Roles.Any())
            {
                var roles = new List<Role>
                {
                    new Role
                    {
                        RoleName = "Customer",
                        Description = "Can browse products, place orders, and manage profile",
                        IsActive = true
                    },
                    new Role
                    {
                        RoleName = "StoreOwner",
                        Description = "Can manage store, products, orders, and analytics",
                        IsActive = true
                    },
                    new Role
                    {
                        RoleName = "DeliveryStaff",
                        Description = "Can manage assigned deliveries and update status",
                        IsActive = true
                    },
                    new Role
                    {
                        RoleName = "Admin",
                        Description = "Full system control - manage users, stores, categories, and platform operations",
                        IsActive = true
                    }
                };

                await context.Roles.AddRangeAsync(roles);
                await context.SaveChangesAsync();
            }

            // =============================================
            // 2. SEED ADMIN USER
            // =============================================
            if (!context.Users.Any(u => u.Email == "admin@marketplace.com"))
            {
                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");

                if (adminRole != null)
                {
                    var adminUser = new User
                    {
                        FullName = "System Administrator",
                        Email = "admin@marketplace.com",
                        PhoneNumber = "+1234567890",
                        PasswordHash = HashPassword("Admin@123"),
                        RoleID = adminRole.RoleID,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = null
                    };

                    await context.Users.AddAsync(adminUser);
                    await context.SaveChangesAsync();
                }
            }

            // =============================================
            // 3. SEED CATEGORIES
            // =============================================
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    // Main Categories (Parent)
                    new Category
                    {
                        CategoryName = "Electronics",
                        CategorySlug = "electronics",
                        Description = "Electronic devices and accessories",
                        DisplayOrder = 1,
                        IsActive = true,
                        ParentCategoryID = null
                    },
                    new Category
                    {
                        CategoryName = "Clothing",
                        CategorySlug = "clothing",
                        Description = "Fashion and apparel",
                        DisplayOrder = 2,
                        IsActive = true,
                        ParentCategoryID = null
                    },
                    new Category
                    {
                        CategoryName = "Groceries",
                        CategorySlug = "groceries",
                        Description = "Fresh food and daily essentials",
                        DisplayOrder = 3,
                        IsActive = true,
                        ParentCategoryID = null
                    },
                    new Category
                    {
                        CategoryName = "Home & Living",
                        CategorySlug = "home-living",
                        Description = "Furniture, decor, and household items",
                        DisplayOrder = 4,
                        IsActive = true,
                        ParentCategoryID = null
                    },
                    new Category
                    {
                        CategoryName = "Beauty & Health",
                        CategorySlug = "beauty-health",
                        Description = "Cosmetics, skincare, and wellness",
                        DisplayOrder = 5,
                        IsActive = true,
                        ParentCategoryID = null
                    },
                    
                    // Subcategories (Children) - Parent IDs will be set after first save
                    new Category
                    {
                        CategoryName = "Smartphones",
                        CategorySlug = "smartphones",
                        Description = "Mobile phones and accessories",
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new Category
                    {
                        CategoryName = "Laptops",
                        CategorySlug = "laptops",
                        Description = "Notebooks and computers",
                        DisplayOrder = 2,
                        IsActive = true
                    },
                    new Category
                    {
                        CategoryName = "Men's Clothing",
                        CategorySlug = "mens-clothing",
                        Description = "Men's fashion",
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new Category
                    {
                        CategoryName = "Women's Clothing",
                        CategorySlug = "womens-clothing",
                        Description = "Women's fashion",
                        DisplayOrder = 2,
                        IsActive = true
                    },
                    new Category
                    {
                        CategoryName = "Fresh Vegetables",
                        CategorySlug = "fresh-vegetables",
                        Description = "Fresh organic vegetables",
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new Category
                    {
                        CategoryName = "Dairy Products",
                        CategorySlug = "dairy-products",
                        Description = "Milk, cheese, butter, and eggs",
                        DisplayOrder = 2,
                        IsActive = true
                    }
                };

                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();

                // Update subcategories with parent IDs
                var electronics = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Electronics");
                var clothing = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Clothing");
                var groceries = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Groceries");

                if (electronics != null)
                {
                    var smartphones = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Smartphones");
                    var laptops = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Laptops");
                    if (smartphones != null) smartphones.ParentCategoryID = electronics.CategoryID;
                    if (laptops != null) laptops.ParentCategoryID = electronics.CategoryID;
                }

                if (clothing != null)
                {
                    var mens = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Men's Clothing");
                    var womens = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Women's Clothing");
                    if (mens != null) mens.ParentCategoryID = clothing.CategoryID;
                    if (womens != null) womens.ParentCategoryID = clothing.CategoryID;
                }

                if (groceries != null)
                {
                    var vegetables = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Fresh Vegetables");
                    var dairy = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Dairy Products");
                    if (vegetables != null) vegetables.ParentCategoryID = groceries.CategoryID;
                    if (dairy != null) dairy.ParentCategoryID = groceries.CategoryID;
                }

                await context.SaveChangesAsync();
            }

            // =============================================
            // 4. SEED SAMPLE STORE
            // =============================================
            if (!context.Stores.Any())
            {
                // First, create a Store Owner user
                var storeOwnerRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "StoreOwner");

                if (storeOwnerRole != null && !context.Users.Any(u => u.Email == "store@example.com"))
                {
                    var storeOwner = new User
                    {
                        FullName = "John Store Owner",
                        Email = "store@example.com",
                        PhoneNumber = "+1987654321",
                        PasswordHash = HashPassword("Store@123"),
                        RoleID = storeOwnerRole.RoleID,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Users.AddAsync(storeOwner);
                    await context.SaveChangesAsync();

                    // Create Store
                    var store = new Store
                    {
                        OwnerUserID = storeOwner.UserID,
                        StoreName = "TechHub Electronics",
                        StoreCode = "TECH001",
                        Description = "Your one-stop shop for all electronics",
                        LogoURL = "/images/stores/techhub-logo.png",
                        PhoneNumber = "+1122334455",
                        Email = "contact@techhub.com",
                        AddressLine1 = "123 Tech Street",
                        AddressLine2 = "Downtown",
                        City = "New York",
                        Area = "Manhattan",
                        Latitude = 40.7128m,
                        Longitude = -74.0060m,
                        BusinessLicenseNumber = "BL123456",
                        Rating = 0,
                        TotalRatings = 0,
                        Status = "Active",
                        CommissionRate = 10.0m,
                        CODSupported = true,
                        CODMaxLimit = 5000,
                        CreatedAt = DateTime.UtcNow,
                        ApprovedAt = DateTime.UtcNow,
                        ApprovedBy = 1
                    };

                    await context.Stores.AddAsync(store);
                    await context.SaveChangesAsync();

                    // Add Delivery Areas for the store
                    var deliveryAreas = new List<DeliveryArea>
                    {
                        new DeliveryArea
                        {
                            StoreID = store.StoreID,
                            AreaName = "Downtown Zone",
                            BoundaryType = "Radius",
                            RadiusKm = 5,
                            BaseDeliveryFee = 2.99m,
                            FeePerKm = 0.50m,
                            FreeDeliveryThreshold = 50,
                            IsActive = true
                        },
                        new DeliveryArea
                        {
                            StoreID = store.StoreID,
                            AreaName = "Suburban Zone",
                            BoundaryType = "Radius",
                            RadiusKm = 10,
                            BaseDeliveryFee = 4.99m,
                            FeePerKm = 0.75m,
                            FreeDeliveryThreshold = 100,
                            IsActive = true
                        }
                    };

                    await context.DeliveryAreas.AddRangeAsync(deliveryAreas);
                    await context.SaveChangesAsync();

                    // Add Sample Products
                    var electronicsCategory = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Electronics");
                    var smartphonesCategory = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Smartphones");

                    var products = new List<Product>
                    {
                        new Product
                        {
                            StoreID = store.StoreID,
                            CategoryID = smartphonesCategory?.CategoryID ?? electronicsCategory?.CategoryID ?? 1,
                            ProductName = "iPhone 15 Pro",
                            ProductSlug = "iphone-15-pro",
                            Description = "Latest iPhone with A17 Pro chip, titanium design",
                            Price = 999.99m,
                            CompareAtPrice = 1099.99m,
                            Quantity = 50,
                            LowStockThreshold = 10,
                            Weight = 0.5m,
                            IsActive = true,
                            Rating = 0,
                            TotalRatings = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Product
                        {
                            StoreID = store.StoreID,
                            CategoryID = electronicsCategory?.CategoryID ?? 1,
                            ProductName = "Samsung 4K Smart TV",
                            ProductSlug = "samsung-4k-tv",
                            Description = "55-inch 4K UHD Smart TV with HDR",
                            Price = 599.99m,
                            CompareAtPrice = 799.99m,
                            Quantity = 30,
                            LowStockThreshold = 5,
                            Weight = 15.0m,
                            IsActive = true,
                            Rating = 0,
                            TotalRatings = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Product
                        {
                            StoreID = store.StoreID,
                            CategoryID = electronicsCategory?.CategoryID ?? 1,
                            ProductName = "Sony Wireless Headphones",
                            ProductSlug = "sony-wireless-headphones",
                            Description = "Noise-cancelling Bluetooth headphones",
                            Price = 199.99m,
                            CompareAtPrice = 299.99m,
                            Quantity = 100,
                            LowStockThreshold = 20,
                            Weight = 0.3m,
                            IsActive = true,
                            Rating = 0,
                            TotalRatings = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    };

                    await context.Products.AddRangeAsync(products);
                    await context.SaveChangesAsync();

                    // Add Product Images
                    var productImages = new List<ProductImage>();
                    var firstProduct = products.First();

                    for (int i = 1; i <= 3; i++)
                    {
                        productImages.Add(new ProductImage
                        {
                            ProductID = firstProduct.ProductID,
                            ImageURL = $"/images/products/product-{firstProduct.ProductID}-{i}.jpg",
                            DisplayOrder = i,
                            IsPrimary = (i == 1)
                        });
                    }

                    await context.ProductImages.AddRangeAsync(productImages);
                    await context.SaveChangesAsync();
                }
            }

            // =============================================
            // 5. SEED SYSTEM CONFIGURATION
            // =============================================
            if (!context.SystemConfigs.Any())
            {
                var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@marketplace.com");
                var adminId = adminUser?.UserID ?? 1;

                var configs = new List<SystemConfig>
                {
                    new SystemConfig
                    {
                        ConfigKey = "GlobalCommissionRate",
                        ConfigValue = "10",
                        ValueType = "Decimal",
                        Description = "Default commission rate for all stores (percentage)",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "DefaultDeliveryFee",
                        ConfigValue = "3.99",
                        ValueType = "Decimal",
                        Description = "Default delivery fee when distance cannot be calculated",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "SessionTimeoutMinutes",
                        ConfigValue = "30",
                        ValueType = "Integer",
                        Description = "User session timeout in minutes",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "MaxCartItems",
                        ConfigValue = "50",
                        ValueType = "Integer",
                        Description = "Maximum items allowed in a single cart",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "CartExpirationDays",
                        ConfigValue = "7",
                        ValueType = "Integer",
                        Description = "Number of days before an abandoned cart expires",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "LowStockAlertThreshold",
                        ConfigValue = "5",
                        ValueType = "Integer",
                        Description = "Default threshold for low stock alerts",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "MaxWishlistItems",
                        ConfigValue = "100",
                        ValueType = "Integer",
                        Description = "Maximum items allowed in a customer's wishlist",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new SystemConfig
                    {
                        ConfigKey = "FreeDeliveryMinimumAmount",
                        ConfigValue = "50",
                        ValueType = "Decimal",
                        Description = "Minimum order amount for free delivery",
                        UpdatedBy = adminId,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await context.SystemConfigs.AddRangeAsync(configs);
                await context.SaveChangesAsync();
            }

            // =============================================
            // 6. SEED SAMPLE CUSTOMER
            // =============================================
            if (!context.Customers.Any())
            {
                var customerRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");

                if (customerRole != null && !context.Users.Any(u => u.Email == "customer@example.com"))
                {
                    var customerUser = new User
                    {
                        FullName = "Jane Customer",
                        Email = "customer@example.com",
                        PhoneNumber = "+15551234567",
                        PasswordHash = HashPassword("Customer@123"),
                        RoleID = customerRole.RoleID,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Users.AddAsync(customerUser);
                    await context.SaveChangesAsync();

                    var customer = new Customer
                    {
                        UserID = customerUser.UserID,
                        DateOfBirth = new DateTime(1990, 1, 15),
                        Gender = "Female",
                        IsVerified = true,
                        LoyaltyPoints = 100,
                        CODBlocked = false
                    };

                    await context.Customers.AddAsync(customer);
                    await context.SaveChangesAsync();

                    // Add sample address for customer
                    var address = new CustomerAddress
                    {
                        CustomerID = customer.CustomerID,
                        AddressLine1 = "456 Oak Street",
                        AddressLine2 = "Apt 3B",
                        City = "New York",
                        Area = "Brooklyn",
                        PostalCode = "11201",
                        Latitude = 40.6782,
                        Longitude = -73.9442,
                        IsDefault = true,
                        IsActive = true
                    };

                    await context.CustomerAddresses.AddAsync(address);
                    await context.SaveChangesAsync();

                    // Update customer's default address
                    customer.DefaultAddressID = address.AddressID;
                    await context.SaveChangesAsync();
                }
            }

            // =============================================
            // 7. SEED SAMPLE DELIVERY PERSON
            // =============================================
            if (!context.DeliveryPersons.Any())
            {
                var deliveryRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "DeliveryStaff");

                if (deliveryRole != null && !context.Users.Any(u => u.Email == "delivery@example.com"))
                {
                    var deliveryUser = new User
                    {
                        FullName = "Mike Delivery",
                        Email = "delivery@example.com",
                        PhoneNumber = "+19998887777",
                        PasswordHash = HashPassword("Delivery@123"),
                        RoleID = deliveryRole.RoleID,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Users.AddAsync(deliveryUser);
                    await context.SaveChangesAsync();

                    var deliveryPerson = new DeliveryPerson
                    {
                        UserID = deliveryUser.UserID,
                        VehicleType = "Bike",
                        VehicleNumber = "BIKE-123",
                        DrivingLicenseNumber = "DL12345678",
                        Status = "Available",
                        Rating = 0,
                        IsActive = true,
                        ApprovedAt = DateTime.UtcNow
                    };

                    await context.DeliveryPersons.AddAsync(deliveryPerson);
                    await context.SaveChangesAsync();
                }
            }

            // =============================================
            // 8. CREATE AUDIT LOG FOR INITIALIZATION
            // =============================================
            if (!context.AuditLogs.Any())
            {
                var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@marketplace.com");

                if (adminUser != null)
                {
                    var auditLog = new AuditLog
                    {
                        UserID = adminUser.UserID,
                        Action = "System Initialized",
                        EntityName = "Database",
                        EntityID = "Seed",
                        IPAddress = "127.0.0.1",
                        UserAgent = "System",
                        ActionDate = DateTime.UtcNow
                    };

                    await context.AuditLogs.AddAsync(auditLog);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}