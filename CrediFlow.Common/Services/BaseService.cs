using Microsoft.EntityFrameworkCore;
using CrediFlow.Common.Caching;

namespace CrediFlow.Common.Services
{
    public abstract class BaseService<TModel, TDbContext> : IBaseService<TModel> where TModel : class where TDbContext : DbContext
    {
        public readonly TDbContext DbContext;

        public readonly ICachingHelper CachingHelper;

        public readonly IUserInfoService User;

        public BaseService(TDbContext db, ICachingHelper cachingHelper, IUserInfoService userInfo)
        {
            DbContext = db;
            CachingHelper = cachingHelper;
            User = userInfo;
        }

        public virtual TModel Get(object id)
        {
            return DbContext.Set<TModel>().Find(id);
        }

        public virtual async Task<TModel> GetAsync(object id)
        {
            return await DbContext.Set<TModel>().FindAsync(id);
        }

        public virtual IQueryable<TModel> GetAll()
        {
            return from c in DbContext.Set<TModel>()
                   select (c);
        }

        public virtual void Add(TModel obj)
        {
            DbContext.Set<TModel>().AddAsync(obj);
        }

        public virtual async Task Save()
        {
            await DbContext.SaveChangesAsync();
        }

        public virtual void Delete(TModel obj)
        {
            DbContext.Set<TModel>().Remove(obj);
        }
    }
}
