using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using Multi_Store.Services.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
  public class CategoryManager
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;

        public CategoryManager(
            ICategoryRepository categoryRepository,
            IMapper mapper)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
        }

        // =========================
        // ADD CATEGORY
        // =========================
        public async Task<CategoryDTO> AddCategoryAsync(CategoryDTO dto)
        {
            var category = _mapper.Map<Category>(dto);

            category.IsActive = true;

            await _categoryRepository.AddAsync(category);

            return _mapper.Map<CategoryDTO>(category);
        }

        // =========================
        // UPDATE CATEGORY
        // =========================
        public async Task<CategoryDTO> UpdateCategoryAsync(CategoryDTO dto)
        {
            var category = await _categoryRepository.GetByIdAsync(dto.CategoryID);

            if (category == null)
                throw new Exception("Category not found");

            category.CategoryName = dto.CategoryName;
            category.CategorySlug = dto.CategorySlug;
            category.Description = dto.Description;
            category.ImageURL = dto.ImageURL;
            category.DisplayOrder = dto.DisplayOrder;
            category.IsActive = dto.IsActive;

            await _categoryRepository.UpdateAsync(category);

            return _mapper.Map<CategoryDTO>(category);
        }

        // =========================
        // MAIN CATEGORIES (ROOT)
        // =========================
        public async Task<IReadOnlyList<CategoryDTO>> GetMainCategoriesAsync()
        {
            var categories = await _categoryRepository.GetMainCategoriesAsync();
            return _mapper.Map<IReadOnlyList<CategoryDTO>>(categories);
        }

        // =========================
        // SUBCATEGORIES
        // =========================
        public async Task<IReadOnlyList<CategoryDTO>> GetSubCategoriesAsync(int parentId)
        {
            var categories = await _categoryRepository.GetSubCategoriesAsync(parentId);
            return _mapper.Map<IReadOnlyList<CategoryDTO>>(categories);
        }

        // =========================
        // CATEGORY WITH PRODUCTS
        // =========================
        public async Task<CategoryDTO> GetCategoryWithProductsAsync(int categoryId)
        {
            var category = await _categoryRepository.GetCategoryWithProductsAsync(categoryId);

            if (category == null)
                throw new Exception("Category not found");

            return _mapper.Map<CategoryDTO>(category);
        }

        // =========================
        // GET BY ID
        // =========================
        public async Task<CategoryDTO> GetByIdAsync(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);

            if (category == null)
                throw new Exception("Category not found");

            return _mapper.Map<CategoryDTO>(category);
        }
    }
}
