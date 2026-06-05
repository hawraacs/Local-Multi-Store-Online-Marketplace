using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Multi_Store.Core.Reposinterface.Base;
using Multi_Store.Infrastructure.Data;
using System.Linq.Expressions;

namespace Multi_Store.Infrastructure.Repositories.Base
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _dbContext;
        private readonly ILogger<Repository<T>>? _logger;

        public Repository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _logger = null;
        }

        public Repository(
            ApplicationDbContext dbContext,
            ILogger<Repository<T>> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<T> GetByIdAsync(int id)
        {
            var entity = await _dbContext.Set<T>().FindAsync(id);

            if (entity == null)
            {
                throw new Exception($"{typeof(T).Name} with ID {id} was not found.");
            }

            return entity;
        }

        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>()
                .ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>()
                .Where(predicate)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            string includeString,
            bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (disableTracking)
            {
                query = query.AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(includeString))
            {
                query = query.Include(includeString);
            }

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (orderBy != null)
            {
                return await orderBy(query).ToListAsync();
            }

            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAsync(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            List<Expression<Func<T, object>>> includes,
            bool disableTracking = true)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (disableTracking)
            {
                query = query.AsNoTracking();
            }

            if (includes != null && includes.Any())
            {
                query = includes.Aggregate(
                    query,
                    (current, include) => current.Include(include));
            }

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (orderBy != null)
            {
                return await orderBy(query).ToListAsync();
            }

            return await query.ToListAsync();
        }

        public async Task<T> AddAsync(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            await _dbContext.Set<T>().AddAsync(entity);

            _logger?.LogInformation("BEFORE SAVE {EntityName}", typeof(T).Name);

            await _dbContext.SaveChangesAsync();

            _logger?.LogInformation("AFTER SAVE {EntityName}", typeof(T).Name);

            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            _dbContext.Entry(entity).State = EntityState.Modified;

            _logger?.LogInformation("BEFORE UPDATE {EntityName}", typeof(T).Name);

            await _dbContext.SaveChangesAsync();

            _logger?.LogInformation("AFTER UPDATE {EntityName}", typeof(T).Name);
        }

        public async Task DeleteAsync(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            _dbContext.Set<T>().Remove(entity);

            _logger?.LogInformation("BEFORE DELETE {EntityName}", typeof(T).Name);

            await _dbContext.SaveChangesAsync();

            _logger?.LogInformation("AFTER DELETE {EntityName}", typeof(T).Name);
        }
    }
}