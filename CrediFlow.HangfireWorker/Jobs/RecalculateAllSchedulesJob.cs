using CrediFlow.API.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CrediFlow.HangfireWorker.Jobs
{
    public class RecalculateAllSchedulesJob : ILoanScheduleRecalculationJob
    {
        private readonly ILoanContractService _loanContractService;
        private readonly ILogger<RecalculateAllSchedulesJob> _logger;

        public RecalculateAllSchedulesJob(
            ILoanContractService loanContractService,
            ILogger<RecalculateAllSchedulesJob> logger)
        {
            _loanContractService = loanContractService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 0)]
        [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Start Hangfire job: RecalculateAllSchedules");

            var updatedCount = await _loanContractService.RecalculateAllPendingSchedulesAsync();

            _logger.LogInformation(
                "Finish Hangfire job: RecalculateAllSchedules. UpdatedCount={UpdatedCount}",
                updatedCount);
        }
    }
}
