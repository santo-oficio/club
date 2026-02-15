using System.Linq.Expressions;
using MexClub.Domain.Entities;
using MexClub.Domain.Interfaces;
using MexClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MexClub.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly MexClubDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(MexClubDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.AnyAsync(predicate, ct);

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate == null
            ? await _dbSet.CountAsync(ct)
            : await _dbSet.CountAsync(predicate, ct);

    public IQueryable<T> Query() => _dbSet.AsNoTracking();
}
