namespace CrediFlow.Common.Services
{
    public interface IBaseService<TModel>
    {
        TModel Get(object id);

        Task<TModel> GetAsync(object id);

        IQueryable<TModel> GetAll();

        void Add(TModel obj);

        void Delete(TModel obj);

        Task Save();
    }
}
