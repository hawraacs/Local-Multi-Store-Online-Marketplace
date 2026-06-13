using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Multi_Store.Core.ViewModels.StoreOwner
{
    public class ProductViewModel
    {


        public int ProductID { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Product name must be between 3 and 200 characters")]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 999999.99, ErrorMessage = "Price must be between $0.01 and $999,999.99")]
        [DataType(DataType.Currency)]
        [Display(Name = "Selling Price")]
        public decimal Price { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Compare at Price (Original Display Price)")]
        public decimal? CompareAtPrice { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Cost Price")]
        [Range(0, 999999.99, ErrorMessage = "Cost price must be valid")]
        public decimal? OriginalPrice { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0, 999999, ErrorMessage = "Quantity must be between 0 and 999,999")]
        [Display(Name = "Stock Quantity")]
        public int Quantity { get; set; }

        [Display(Name = "Low Stock Threshold")]
        [Range(0, 1000)]
        public int LowStockThreshold { get; set; } = 5;

        [Display(Name = "Weight (kg)")]
        [Range(0, 1000)]
        public decimal? Weight { get; set; }

        public int CategoryID { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Category")]
        public string CategoryName { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Product Images")]
        public List<IFormFile>? UploadedImages { get; set; }

        public List<ProductImageViewModel>? ExistingImages { get; set; }

        public string? PrimaryImageUrl { get; set; }

        public bool IsOutOfStock => Quantity <= 0;

        public decimal? DiscountPercentage =>
            CompareAtPrice.HasValue && CompareAtPrice > Price
                ? Math.Round(((CompareAtPrice.Value - Price) / CompareAtPrice.Value) * 100, 0)
                : null;

        public decimal AdminFeePerUnit => Math.Round(Price * 0.05m, 2);

        public decimal? NetProfitPerUnit =>
            OriginalPrice.HasValue
                ? Price - OriginalPrice.Value - AdminFeePerUnit
                : null;
    }

    public class ProductImageViewModel
    {
        public int ImageID { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class ProductListViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal? CompareAtPrice { get; set; }

        public decimal? OriginalPrice { get; set; }

        public int Quantity { get; set; }

        public int LowStockThreshold { get; set; }

        public bool IsActive { get; set; }

        public bool IsOutOfStock { get; set; }

        public decimal Rating { get; set; }

        public string? PrimaryImageUrl { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public decimal? DiscountPercentage =>
            CompareAtPrice.HasValue && CompareAtPrice > Price
                ? Math.Round(((CompareAtPrice.Value - Price) / CompareAtPrice.Value) * 100, 0)
                : null;

        public decimal? ProfitPerUnit =>
            OriginalPrice.HasValue
                ? Price - OriginalPrice.Value
                : null;

        public decimal? MarginPercent =>
            OriginalPrice.HasValue && Price > 0
                ? Math.Round(((Price - OriginalPrice.Value) / Price) * 100, 2)
                : null;

        public decimal AdminFeePerUnit => Math.Round(Price * 0.05m, 2);

        public decimal? NetProfitPerUnit =>
            OriginalPrice.HasValue
                ? Price - OriginalPrice.Value - AdminFeePerUnit
                : null;

        public string StockStatus =>
            IsOutOfStock ? "Out of Stock"
            : (Quantity <= LowStockThreshold ? "Low Stock" : "In Stock");

        public string StockStatusClass =>
            IsOutOfStock ? "out"
            : (Quantity <= LowStockThreshold ? "low" : "normal");
    }
}