using CrediFlow.Common.Models;
using System.Data;

namespace CrediFlow.Common.Base
{
    public interface IBaseReposDP<TModel>
    {
        IDbConnection GetOpenConnection();

        IMethodResult<IEnumerable<TModel>> GetsBySearch(string keyword, short trangThai, int pageIndex, int pageSize, string orderCol, bool isDesc);

        IMethodResult<TModel> GetById(long id);

        IMethodResult<long> Insert(TModel model);

        IResultAPI InsertMany(List<TModel> models);

        IResultAPI Delete(long id, long UserId);

        IResultAPI DeleteMany(string ids, long UserId);

        IResultAPI Update(TModel model);
    }
}
