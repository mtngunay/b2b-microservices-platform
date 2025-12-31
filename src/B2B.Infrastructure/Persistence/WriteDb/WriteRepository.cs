using B2B.Application.Interfaces.Persistence;
using B2B.Domain.Entities;
using B2B.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// Generic repository implementation for write operations using EF Core.
/// </summary>
/// <typeparam name="TEntity">The aggregate root entity type.</typeparam>
/// <typeparam name="TId">The entity identifier type.</typeparam>
public class WriteRepository<TEntity, TId> : IWriteRepository<TEntity, TId>
    where TEntity : class, IAggregateRoot<TId>
{
    protected readonly WriteDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    /// <summary>
    /// Initializes a new instance of the WriteRepository.
    /// </summary>
    /// <param name="context">The database context.</param>
    public WriteRepository(WriteDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = context.Set<TEntity>();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object?[] { id }, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdWithIncludesAsync(
        TId id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes)
    {
        IQueryable<TEntity> query = _dbSet;

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        // Assuming TId is Guid for most cases
        return await query.FirstOrDefaultAsync(
            e => EF.Property<TId>(e, "Id")!.Equals(id),
            cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        // Check if entity supports soft delete
        if (entity is BaseEntity baseEntity)
        {
            baseEntity.MarkAsDeleted();
            _dbSet.Update(entity);
        }
        else
        {
            _dbSet.Remove(entity);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> AsQueryable()
    {
        return _dbSet.AsQueryable();
    }
}
