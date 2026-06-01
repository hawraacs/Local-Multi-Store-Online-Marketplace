using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Compare at Price (Original Price)")]
        public decimal? CompareAtPrice { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0, 999999, ErrorMessage = "Quantity must be between 0 and 999,999")]
        [Display(Name = "Stock Quantity")]
        public int Quantity { get; set; }

        [Display(Name = "Low Stock Threshold")]
        [Range(0, 1000, ErrorMessage = "Low stock threshold must be between 0 and 1000")]
        public int LowStockThreshold { get; set; } = 5;

        [Display(Name = "Weight (kg)")]
        [Range(0, 1000, ErrorMessage = "Weight must be between 0 and 1000 kg")]
        public decimal? Weight { get; set; }

        [Required(ErrorMessage = "Please select a category")]
        public int CategoryID { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        // Image Upload
        [Display(Name = "Product Images")]
        public List<IFormFile>? UploadedImages { get; set; }

        public List<ProductImageViewModel>? ExistingImages { get; set; }

        // Display Properties
        public string? PrimaryImageUrl { get; set; }
        public bool IsOutOfStock => Quantity <= 0;
        public decimal? DiscountPercentage => CompareAtPrice.HasValue && CompareAtPrice > Price
            ? Math.Round(((CompareAtPrice.Value - Price) / CompareAtPrice.Value) * 100, 0)
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
        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; }
        public bool IsActive { get; set; }
        public bool IsOutOfStock { get; set; }
        public decimal Rating { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal? DiscountPercentage => CompareAtPrice.HasValue && CompareAtPrice > Price
            ? Math.Round(((CompareAtPrice.Value - Price) / CompareAtPrice.Value) * 100, 0)
            : null;
        public string StockStatus => IsOutOfStock ? "Out of Stock" : (Quantity <= LowStockThreshold ? "Low Stock" : "In Stock");
        public string StockStatusClass => IsOutOfStock ? "out" : (Quantity <= LowStockThreshold ? "low" : "normal");
    }
}