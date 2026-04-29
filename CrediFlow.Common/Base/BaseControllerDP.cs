//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;

namespace CrediFlow.Common.Base
{

    //public abstract class BaseControllerDP<TRepos, TModel> : Controller where TRepos : IBaseReposDP<TModel> where TModel : BaseModel
    //{
    //    protected readonly ILogger<BaseControllerDP<TRepos, TModel>> _logger;

    //    protected readonly IMapper _mapper;

    //    protected readonly TRepos _repos;

    //    protected BaseControllerDP(IMapper mapper, ILogger<BaseControllerDP<TRepos, TModel>> logger, TRepos repos)
    //    {
    //        _logger = logger;
    //        _mapper = mapper;
    //        _repos = repos;
    //    }

    //    [HttpGet]
    //    public virtual IActionResult GetsBySearch(string keyword, short trangThai, int pageIndex, int pageSize, string orderCol, bool isDesc)
    //    {
    //        IResult<IEnumerable<TModel>> result = _repos.GetsBySearch(keyword, trangThai, pageIndex, pageSize, orderCol, isDesc);
    //        return ResponseResult(result);
    //    }

    //    [HttpGet("{id}")]
    //    public virtual IActionResult GetById(long id)
    //    {
    //        IResult<TModel> byId = _repos.GetById(id);
    //        return ResponseResult(byId);
    //    }

    //    [HttpPost]
    //    public virtual IActionResult Post([FromBody] TModel model)
    //    {
    //        IResult methodResult = ValidatePost(model);
    //        if (!methodResult.Success)
    //        {
    //            return BadRequest(methodResult);
    //        }

    //        ((IBaseModel)model).Modified = DateTime.Now;
    //        IResult<long> result = _repos.Insert(model);
    //        return ResponseResult(result);
    //    }

    //    [HttpPut("{id}")]
    //    public virtual IActionResult Put(long id, [FromBody] TModel model)
    //    {
    //        IResult methodResult = ValidatePut(id, model);
    //        if (!methodResult.Success)
    //        {
    //            return BadRequest(methodResult);
    //        }

    //        ((IBaseModel)model).Id = id;
    //        IResult methodResult2 = _repos.Update(model);
    //        return ResponseResult(new MethodResult());
    //    }

    //    [HttpDelete("{id}")]
    //    public virtual IActionResult Delete(long id)
    //    {
    //        IResult methodResult = ValidateDelete(id);
    //        if (!methodResult.Success)
    //        {
    //            return BadRequest(methodResult);
    //        }

    //        IResult result = _repos.Delete(id, 0L);
    //        return ResponseResult(result);
    //    }

    //    [HttpDelete("DeleteMany/{ids}")]
    //    public virtual IActionResult DeleteMany(string ids)
    //    {
    //        IResult result = _repos.DeleteMany(ids, 0L);
    //        return ResponseResult(result);
    //    }

    //    protected virtual IActionResult ResponseResult(IResult result)
    //    {
    //        if (!result.Success)
    //        {
    //            if (result.Status == 403)
    //            {
    //                return Forbid();
    //            }

    //            if (result.Status == 404)
    //            {
    //                return NotFound(result);
    //            }

    //            if (result.Status == 409)
    //            {
    //                return StatusCode(409);
    //            }

    //            return BadRequest(result);
    //        }

    //        return Ok(result);
    //    }

    //    protected virtual IActionResult ResponseResult<T>(IResult<T> result)
    //    {
    //        if (!result.Success)
    //        {
    //            if (result.Status == 403)
    //            {
    //                return Forbid();
    //            }

    //            if (result.Status == 404)
    //            {
    //                return NotFound(result);
    //            }

    //            if (result.Status == 409)
    //            {
    //                return StatusCode(409);
    //            }

    //            return BadRequest(result);
    //        }

    //        return Ok(result);
    //    }

    //    protected virtual IActionResult ResponseWithMappedData<TSourceMap, TTargetMap>(IResult<TSourceMap> result)
    //    {
    //        if (!result.Success)
    //        {
    //            return ResponseResult(result);
    //        }

    //        TTargetMap data = _mapper.Map<TTargetMap>(result.Data);
    //        return Ok(MethodResult<TTargetMap>.ResultWithData(data, "", result.TotalRecord));
    //    }

    //    protected virtual IResult ValidatePost(TModel model)
    //    {
    //        return MethodResult.ResultWithSuccess();
    //    }

    //    protected virtual IResult ValidatePut(long id, TModel model)
    //    {
    //        return MethodResult.ResultWithSuccess();
    //    }

    //    protected virtual IResult ValidateDelete(long id)
    //    {
    //        return MethodResult.ResultWithSuccess();
    //    }
    //}
}
