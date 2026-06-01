using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Infrastructure.Data;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class ProductManager
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductImageRepository _productImageRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IStoreRepository _storeRepository;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;

        public ProductManager(
            IProductRepository productRepository,
            IProductImageRepository productImageRepository,
            ICategoryRepository categoryRepository,
            IStoreRepository storeRepository,
            ApplicationDbContext context,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _productImageRepository = productImageRepository;
            _categoryRepository = categoryRepository;
            _storeRepository = storeRepository;
            _context = context;
            _mapper = mapper;
        }

        // =========================
        // ADD PRODUCT
        // =========================
        public async Task<ProductDTO> AddProductAsync(ProductDTO dto)
        {
            var store = await _storeRepository.GetByIdAsync(dto.StoreID);
            if (store == null)
                throw new Exception("Store not found");

            var category = await _categoryRepository.GetByIdAsync(dto.CategoryID);
            if (category == null)
                throw new Exception("Category not found");

            var product = _mapper.Map<Product>(dto);

            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            product.IsActive = true;

            await _productRepository.AddAsync(product);

            await _context.SaveChangesAsync();

            // Save images
            if (dto.Images != null)
            {
                foreach (var img in dto.Images)
                {
                    var image = new ProductImage
                    {
                        ProductID = product.ProductID,
                        ImageUrl = img.ImageUrl,
                        DisplayOrder = img.DisplayOrder,
                        IsPrimary = img.IsPrimary
                    };

                    await _productImageRepository.AddAsync(image);
                }

                await _context.SaveChangesAsync();
            }

            return _mapper.Map<ProductDTO>(product);
        }

        // =========================
        // UPDATE PRODUCT
        // =========================
        public async Task<ProductDTO> UpdateProductAsync(ProductDTO dto)
        {
            var product = await _productRepository.GetByIdAsync(dto.ProductID);

            if (product == null)
                throw new Exception("Product not found");

            product.ProductName = dto.ProductName;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.CompareAtPrice = dto.CompareAtPrice;
            product.Quantity = dto.Quantity;
            product.LowStockThreshold = dto.LowStockThreshold;
            product.Weight = dto.Weight;
            product.IsActive = dto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product);

            await _context.SaveChangesAsync();

            return _mapper.Map<ProductDTO>(product);
        }

        // =========================
        // GET PRODUCT DETAILS
        // =========================
        public async Task<ProductDTO> GetProductDetailsAsync(int productId)
        {
            var product = await _productRepository.GetProductDetailsAsync(productId);

            if (product == null)
                throw new Exception("Product not found");

            return _mapper.Map<ProductDTO>(product);
        }

        // =========================
        // SEARCH PRODUCTS
        // =========================
        public async Task<IReadOnlyList<ProductDTO>> SearchProductsAsync(string keyword)
        {
            var products = string.IsNullOrWhiteSpace(keyword)
                ? await _productRepository.GetActiveProductsAsync()
                : await _productRepository.SearchProductsAsync(keyword);

            return _mapper.Map<IReadOnlyList<ProductDTO>>(products);
        }

        // =========================
        // GET BY STORE
        // =========================
        public async Task<IReadOnlyList<ProductDTO>> GetByStoreAsync(int storeId)
        {
            var products = await _productRepository.GetByStoreAsync(storeId);
            return _mapper.Map<IReadOnlyList<ProductDTO>>(products);
        }

        // =========================
        // GET BY CATEGORY
        // =========================
        public async Task<IReadOnlyList<ProductDTO>> GetByCategoryAsync(int categoryId)
        {
            var products = await _productRepository.GetByCategoryAsync(categoryId);
            return _mapper.Map<IReadOnlyList<ProductDTO>>(products);
        }

        // =========================
        // LOW STOCK
        // =========================
        public async Task<IReadOnlyList<ProductDTO>> GetLowStockProductsAsync()
        {
            var products = await _productRepository.GetLowStockProductsAsync();
            return _mapper.Map<IReadOnlyList<ProductDTO>>(products);
        }

        // =========================
        // TOP RATED
        // =========================
        public async Task<IReadOnlyList<ProductDTO>> GetTopRatedProductsAsync(int count)
        {
            var products = await _productRepository.GetTopRatedProductsAsync(count);
            return _mapper.Map<IReadOnlyList<ProductDTO>>(products);
        }
    }
}
