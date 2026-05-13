using Hangfire;
using System.ComponentModel;

namespace CrediFlow.API.Services
{
    public interface ILoanScheduleRecalculationJob
    {
        [DisplayName("RecalculateAllSchedulesJob")]
        [Queue("maintenance")]
        Task ExecuteAsync();
    }
}
