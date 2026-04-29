using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public class CULoanCollateralModel
    {
        public Guid? CollateralId { get; set; }
        public Guid LoanContractId { get; set; }
        public string CollateralType { get; set; } = "PHONE";
        public string Description { get; set; } = null!;
        /// <summary>Số IMEI (điện thoại), biển số (xe cộ), số sổ tiết kiệm...</summary>
        public string? SerialNumber { get; set; }
        public decimal? EstimatedValue { get; set; }
        public string? Detail { get; set; }
        public string? Note { get; set; }
    }

    public interface ILoanCollateralService : IBaseService<LoanCollateral>
    {
        Task<IList<LoanCollateral>> GetByLoanContract(Guid loanContractId);
        Task<LoanCollateral> Save(CULoanCollateralModel model);
        Task Delete(Guid collateralId);
    }

    public class LoanCollateralService : BaseService<LoanCollateral, CrediflowContext>, ILoanCollateralService
    {
        public LoanCollateralService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        public async Task<IList<LoanCollateral>> GetByLoanContract(Guid loanContractId)
        {
            var loan = await DbContext.LoanContracts.FindAsync(loanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khoản vay với Id = {loanContractId}");

            if (!User.IsAdmin && loan.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền xem khoản vay thuộc chi nhánh khác.");

            return await DbContext.LoanCollaterals
                .Where(c => c.LoanContractId == loanContractId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<LoanCollateral> Save(CULoanCollateralModel model)
        {
            var loan = await DbContext.LoanContracts.FindAsync(model.LoanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy khoản vay với Id = {model.LoanContractId}");

            if (!User.IsAdmin && loan.StoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền chỉnh sửa khoản vay thuộc chi nhánh khác.");

            if (loan.StatusCode != "DRAFT")
                throw new InvalidOperationException("Chỉ được thêm/sửa tài sản đảm bảo khi khoản vay ở trạng thái DRAFT.");

            if (model.CollateralId == null || model.CollateralId == Guid.Empty)
            {
                // Tạo mới
                var entity = new LoanCollateral
                {
                    CollateralId   = Guid.CreateVersion7(),
                    LoanContractId = model.LoanContractId,
                    CollateralType = model.CollateralType,
                    Description    = model.Description,
                    SerialNumber   = string.IsNullOrWhiteSpace(model.SerialNumber) ? null : model.SerialNumber,
                    EstimatedValue = model.EstimatedValue,
                    Detail         = string.IsNullOrWhiteSpace(model.Detail)  ? null : model.Detail,
                    Note           = string.IsNullOrWhiteSpace(model.Note)    ? null : model.Note,
                    CreatedBy      = CommonLib.GetGUID(User.UserId),
                    CreatedAt      = DateTime.Now,
                };
                DbContext.LoanCollaterals.Add(entity);
                await DbContext.SaveChangesAsync();
                return entity;
            }
            else
            {
                // Cập nhật
                var entity = await DbContext.LoanCollaterals.FindAsync(model.CollateralId)
                    ?? throw new KeyNotFoundException($"Không tìm thấy tài sản đảm bảo với Id = {model.CollateralId}");

                entity.CollateralType = model.CollateralType;
                entity.Description    = model.Description;
                entity.SerialNumber   = string.IsNullOrWhiteSpace(model.SerialNumber) ? null : model.SerialNumber;
                entity.EstimatedValue = model.EstimatedValue;
                entity.Detail         = string.IsNullOrWhiteSpace(model.Detail) ? null : model.Detail;
                entity.Note           = string.IsNullOrWhiteSpace(model.Note)   ? null : model.Note;
                await DbContext.SaveChangesAsync();
                return entity;
            }
        }

        public async Task Delete(Guid collateralId)
        {
            var result = await DbContext.LoanCollaterals
                .Where(c => c.CollateralId == collateralId)
                .Select(c => new { Meta = c, ContractStoreId = c.LoanContract.StoreId, ContractStatus = c.LoanContract.StatusCode })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Không tìm thấy tài sản đảm bảo với Id = {collateralId}");

            if (!User.IsAdmin && result.ContractStoreId != User.StoreId)
                throw new UnauthorizedAccessException("Không có quyền xóa tài sản đảm bảo thuộc chi nhánh khác.");

            if (result.ContractStatus != "DRAFT")
                throw new InvalidOperationException("Chỉ được xóa tài sản đảm bảo khi khoản vay ở trạng thái DRAFT.");

            DbContext.LoanCollaterals.Remove(result.Meta);
            await DbContext.SaveChangesAsync();
        }
    }
}
