// Data/SeedData.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Multi_Store.Core.Entities;
using System.Security.Cryptography;
using System.Text;

namespace Multi_Store.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

            await context.Database.EnsureCreatedAsync();

            // =========================
            // 1. CREATE ROLES (Identity)
            // =========================
            string[] roles = { "Admin", "StoreOwner", "Customer", "Delivery" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(role));
                }
            }

            // =========================
            // 2. ADMIN USER
            // =========================
            if (await userManager.FindByEmailAsync("admin@marketplace.com") == null)
            {
                var admin = new User
                {
                    UserName = "admin@marketplace.com",
                    Email = "admin@marketplace.com",
                    FullName = "System Admin",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(admin, "Admin@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }

            // =========================
            // 3. STORE OWNER
            // =========================
            if (await userManager.FindByEmailAsync("store@example.com") == null)
            {
                var storeOwner = new User
                {
                    UserName = "store@example.com",
                    Email = "store@example.com",
                    FullName = "Store Owner",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(storeOwner, "Store@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(storeOwner, "StoreOwner");
                }
            }

            // =========================
            // 4. CUSTOMER
            // =========================
            if (await userManager.FindByEmailAsync("customer@example.com") == null)
            {
                var customer = new User
                {
                    UserName = "customer@example.com",
                    Email = "customer@example.com",
                    FullName = "Jane Customer",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(customer, "Customer@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(customer, "Customer");
                }
            }

            // =========================
            // 5. DELIVERY
            // =========================
            if (await userManager.FindByEmailAsync("delivery@example.com") == null)
            {
                var delivery = new User
                {
                    UserName = "delivery@example.com",
                    Email = "delivery@example.com",
                    FullName = "Delivery User",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(delivery, "Delivery@123");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(delivery, "Delivery");
                }
            }

            // OPTIONAL: keep your category/product seeding AFTER this
        }
    }
}