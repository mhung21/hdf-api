namespace CrediFlow.Common.Models
{
    public interface IResultAPI
    {
        bool Status { get; set; }

        string Message { get; set; }

        int? Code { get; set; }

        object? Data { get; set; }
    }
}
