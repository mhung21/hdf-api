namespace CrediFlow.Common.Models
{
    public class MethodResult<T> : IMethodResult<T>
    {
        public bool Success { get; set; } = true;


        public T Data { get; set; }

        public string Error { get; set; }

        public string Message { get; set; }

        public int? Status { get; set; }

        public int TotalRecord { get; set; }

        public Guid? CorrelationId { get; set; }

        public static MethodResult<T> ResultWithData(T data, string message = "", int totalRecord = 0, Guid? correlationId = null)
        {
            return new MethodResult<T>
            {
                Data = data,
                Message = message,
                TotalRecord = totalRecord,
                CorrelationId = correlationId
            };
        }

        public static MethodResult<T> ResultWithError(string error, int? status = null, string message = "", Guid? correlationId = null, T data = default(T))
        {
            return new MethodResult<T>
            {
                Success = false,
                Error = error,
                Message = message,
                Status = status,
                CorrelationId = correlationId,
                Data = data
            };
        }

        public static MethodResult<T> ResultWithAccessDenined()
        {
            return ResultWithError("ERR_FORBIDDEN", 403, "Bạn không đủ quyền để lấy dữ liệu đã yêu cầu");
        }

        public static MethodResult<T> ResultWithNotFound()
        {
            return ResultWithError("ERR_NOT_FOUND", 400, "Không tìm thấy dữ liệu đã yêu cầu");
        }

        public MethodResult<TOut> ConvertIfError<TOut>()
        {
            return MethodResult<TOut>.ResultWithError(Error, Status, Message, (Guid?)null, default(TOut));
        }

        public ResultAPI ConvertIfError()
        {
            return ResultAPI.Error(Error, Message, Status);
        }
    }
}
