namespace CrediFlow.Common.Models
{
    public class ResultAPI : IResultAPI
    {
        public bool Status { get; set; } = true;
        public string Message { get; set; }
        public object? Data { get; set; }
        public int? Code { get; set; }

        public static ResultAPI Error(object? data = null, string message = "Thất bại", int? code = 500)
        {
            return new ResultAPI
            {
                Status = false,
                Message = message,
                Data = data,
                Code = code
            };
        }

        public static ResultAPI Success(object? data = null, string message = "Thành công", int? code = 200)
        {
            return new ResultAPI
            {
                Status = true,
                Message = message,
                Data = data,
                Code = code
            };
        }

        public static ResultAPI ResultWithAccessDenined()
        {
            return Error("ERR_FORBIDDEN", "Bạn không đủ quyền để lấy dữ liệu đã yêu cầu", 403);
        }

        public static ResultAPI ResultWithNotFound()
        {
            return Error("ERR_NOT_FOUND", "Không tìm thấy dữ liệu đã yêu cầu", 400);
        }
    }
}
