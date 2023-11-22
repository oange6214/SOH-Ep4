using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SohatNotebook.DataService.Data;
using SohatNotebook.DataService.IRepository;

namespace SohatNotebook.DataService.Repository;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected AppDbContext _context;
    internal DbSet<T> _dbSet;
    protected ILogger _logger;

    public GenericRepository(
        AppDbContext context,
        ILogger logger
        )
    {
        _context = context;
        _logger = logger;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<bool> Add(T entity)
    {
        await _dbSet.AddAsync(entity);
        return true;
    }

    public virtual async Task<IEnumerable<T>> All()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual Task<bool> Delete(Guid id, string userId)
    {
        // 繼承後改寫
        throw new NotImplementedException();
    }

    public virtual async Task<T> GetById(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual Task<bool> Upsert(T entity)
    {
        // 每個 Repo 都是獨特的，將會繼承後改寫
        throw new NotImplementedException();
    }
}