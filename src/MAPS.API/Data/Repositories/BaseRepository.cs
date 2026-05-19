using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MAPS.API.Data.Repositories.Interfaces;
using MAPS.Shared.DTOs.Common;

namespace MAPS.API.Data.Repositories;

public class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public BaseRepository(AppDbContext context)
    {
        _context = context;
        _dbSet   = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id) =>
        await _dbSet.FindAsync(id);

    public virtual async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.ToListAsync();

    public virtual async Task<PagedResult<T>> GetPagedAsync(
        int page, int pageSize,
        Expression<Func<T, bool>>? filter = null)
    {
        var query = filter != null ? _dbSet.Where(filter) : _dbSet.AsQueryable();
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.AnyAsync(predicate);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null) =>
        predicate != null
            ? await _dbSet.CountAsync(predicate)
            : await _dbSet.CountAsync();
}
