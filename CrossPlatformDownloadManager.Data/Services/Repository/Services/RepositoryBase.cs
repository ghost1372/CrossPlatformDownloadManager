using System.Linq.Expressions;
using CrossPlatformDownloadManager.Data.DbContext;
using CrossPlatformDownloadManager.Data.Services.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CrossPlatformDownloadManager.Data.Services.Repository.Services;

public class RepositoryBase<T> : IRepositoryBase<T> where T : class, new()
{
    #region Private Fields

    private readonly DownloadManagerDbContext _dbContext;

    #endregion

    #region Properties

    protected DbSet<T> Table { get; }

    #endregion

    public RepositoryBase(DownloadManagerDbContext dbContext)
    {
        _dbContext = dbContext;
        Table = _dbContext.Set<T>();
    }

    public async Task AddAsync(T? entity)
    {
        if (entity == null)
            return;

        await Table.AddAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<T>? entities)
    {
        var objects = entities?.ToList() ?? [];
        if (!objects.Any())
            return;

        await Table.AddRangeAsync(objects);
    }

    public async Task<T?> GetAsync(Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        params string[] includeProperties)
    {
        return (await GetEntitiesAsync<T>(where: where, orderBy: orderBy, includeProperties: includeProperties))
            .FirstOrDefault();
    }

    public async Task<TR?> GetAsync<TR>(Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<T, TR>? select = null,
        params string[] includeProperties)
    {
        if (select == null)
            return default;

        return (await GetEntitiesAsync(where: where, orderBy: orderBy, select: select,
                includeProperties: includeProperties))
            .FirstOrDefault();
    }

    public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        bool distinct = false,
        params string[] includeProperties)
    {
        return await GetEntitiesAsync<T>(where: where, orderBy: orderBy, distinct: distinct,
            includeProperties: includeProperties);
    }

    public async Task<List<TR>> GetAllAsync<TR>(Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<T, TR>? select = null,
        bool distinct = false,
        params string[] includeProperties)
    {
        if (select == null)
            return [];

        return await GetEntitiesAsync(where: where, orderBy: orderBy, select: select, distinct: distinct,
            includeProperties: includeProperties);
    }

    public void Delete(T? entity)
    {
        if (entity == null)
            return;

        Table.Remove(entity);
    }

    public void DeleteAll(IEnumerable<T>? entities)
    {
        var objects = entities?.ToList() ?? [];
        if (!objects.Any())
            return;

        Table.RemoveRange(objects);
    }

    #region Helpers

    private async Task<List<TR>> GetEntitiesAsync<TR>(Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<T, TR>? select = null,
        bool distinct = false,
        params string[] includeProperties)
    {
        var table = Table.AsQueryable();

        if (where != null)
            table = table.Where(where);

        if (orderBy != null)
            table = orderBy(table);

        if (includeProperties.Any())
        {
            foreach (var includeProperty in includeProperties)
                table = table.Include(includeProperty);
        }

        if (distinct)
            table = table.Distinct();

        var data = await table.ToListAsync();
        var result = select != null ? data.Select(select).ToList() : data.Cast<TR>().ToList();
        return result;
    }

    #endregion
}