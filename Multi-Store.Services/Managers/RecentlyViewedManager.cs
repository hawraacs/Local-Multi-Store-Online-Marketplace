using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;

namespace Multi_Store.Services.Managers
{
    public class RecentlyViewedManager
    {
        private readonly IRecentlyViewedProductRepository _repository;

        public RecentlyViewedManager(
            IRecentlyViewedProductRepository repository)
        {
            _repository = repository;
        }

        public async Task AddViewAsync(
            int customerId,
            int productId)
        {
            await _repository.AddViewAsync(
                customerId,
                productId);
        }

        public async Task<List<RecentlyViewedProductDTO>> GetCustomerRecentlyViewedAsync(int customerId)
        {
            var items = await _repository.GetByCustomerAsync(customerId);

            return items.Select(x => new RecentlyViewedProductDTO
            {
                Id = x.Id,
                CustomerID = x.CustomerID,
                ProductID = x.ProductID,
                ViewedAt = x.ViewedAt,

                ProductName = x.Product.ProductName,
                Price = x.Product.Price,
                StoreName = x.Product.Store.StoreName,
                IsOutOfStock = x.Product.IsOutOfStock,

                ImageUrl = x.Product.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault() ?? "/images/no-image.png"
            }).ToList();
        }
    }
}