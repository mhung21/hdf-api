using CrediFlow.API.Models;
using CrediFlow.Common.Caching;
using CrediFlow.Common.Services;
using CrediFlow.Common.Utils;
using CrediFlow.DataContext.Models;
using Microsoft.EntityFrameworkCore;

namespace CrediFlow.API.Services
{
    public interface ICashVoucherService : IBaseService<CashVoucher>
    {
        /// <summary>Tạo mới hoặc cập nhật phiếu thu/chi.</summary>
        Task<CashVoucher> Save(CUCashVoucherModel model);

        /// <summary>Thu tiền một khoản vay: tạo CashVoucher + phân bổ + cập nhật lịch/phí.</summary>
        Task<CashVoucher> CollectLoanPayment(CollectLoanPaymentModel model);

        /// <summary>Lấy danh sách phiếu thu/chi theo quyền.</summary>
        Task<IList<CashVoucher>> GetAlls();

        /// <summary>Tìm kiếm phiếu thu/chi với phân trang và sắp xếp.</summary>
        Task<object> SearchCashVoucher(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc, Guid? loanContractId = null);

        /// <summary>Lấy toàn bộ phiếu thu/chi thuộc một hợp đồng cụ thể (có allocation).</summary>
        Task<IList<object>> GetByLoanContract(Guid loanContractId);
    }

    public class CashVoucherService : BaseService<CashVoucher, CrediflowContext>, ICashVoucherService
    {
        public CashVoucherService(CrediflowContext dbContext, ICachingHelper cachingHelper, IUserInfoService user)
            : base(dbContext, cachingHelper, user) { }

        private IReadOnlyList<Guid>? GetStoreScopeIds(Guid? storeId = null) => User.GetStoreScopeIds(storeId);

        public async Task<IList<CashVoucher>> GetAlls()
        {
            var query = DbContext.CashVouchers.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Contains(v.StoreId));

            return await query.OrderByDescending(v => v.VoucherDatetime).ToListAsync();
        }

        public async Task<CashVoucher> Save(CUCashVoucherModel model)
        {
            bool isCreate = model.VoucherId == null || model.VoucherId == Guid.Empty;

            // Nếu tạo phiếu thu trả nợ từ màn "Phiếu", đi qua luồng CollectLoanPayment
            // để đảm bảo có allocation và cập nhật lịch trả nợ/phí.
            if (isCreate && TryBuildCollectLoanPaymentModel(model, out var collectModel))
            {
                var voucher = await CollectLoanPayment(collectModel!);

                // Giữ metadata theo dữ liệu người dùng nhập từ form tạo phiếu.
                voucher.BusinessDate         = model.BusinessDate;
                voucher.VoucherDatetime      = model.VoucherDatetime;
                voucher.ReasonCode           = model.ReasonCode;
                voucher.PayerReceiverName    = model.PayerReceiverName    ?? voucher.PayerReceiverName;
                voucher.PayerReceiverAddress = model.PayerReceiverAddress ?? voucher.PayerReceiverAddress;
                voucher.DocumentNo           = model.DocumentNo           ?? voucher.DocumentNo;
                voucher.Description          = model.Description;
                voucher.UpdatedAt            = DateTime.Now;

                await DbContext.SaveChangesAsync();
                return voucher;
            }

            CashVoucher obj;

            if (isCreate)
            {
                obj = new CashVoucher
                {
                    VoucherId  = Guid.CreateVersion7(),
                    VoucherNo  = $"CV-{model.VoucherType}{Random.Shared.Next(0, 99999999)}-{DateTime.Now:yyyyMMddHHmmss}",
                    CreatedBy  = CommonLib.GetGUID(User.UserId),
                };
                DbContext.CashVouchers.Add(obj);
            }
            else
            {
                obj = await DbContext.CashVouchers.FirstOrDefaultAsync(v => v.VoucherId == model.VoucherId)
                      ?? throw new KeyNotFoundException($"Không tìm thấy phiếu với Id = {model.VoucherId}");
            }

            // Xác định chi nhánh: ưu tiên model → JWT → hợp đồng liên quan
            Guid? resolvedStoreId = model.StoreId ?? User.StoreId;
            if (!resolvedStoreId.HasValue && model.LoanContractId.HasValue)
            {
                resolvedStoreId = await DbContext.LoanContracts
                    .Where(c => c.LoanContractId == model.LoanContractId.Value)
                    .Select(c => (Guid?)c.StoreId)
                    .FirstOrDefaultAsync();
            }
            obj.StoreId              = resolvedStoreId
                                       ?? throw new InvalidOperationException("Không xác định được chi nhánh. Vui lòng chọn chi nhánh hoặc liên kết hợp đồng.");
            obj.VoucherType          = model.VoucherType;
            obj.ReasonCode           = model.ReasonCode;
            obj.BusinessDate         = model.BusinessDate;
            obj.VoucherDatetime      = model.VoucherDatetime;
            obj.CustomerId           = model.CustomerId;
            obj.LoanContractId       = model.LoanContractId;
            obj.RelatedVoucherId     = model.RelatedVoucherId     ?? obj.RelatedVoucherId;
            obj.PayerReceiverName    = model.PayerReceiverName    ?? obj.PayerReceiverName;
            obj.PayerReceiverAddress = model.PayerReceiverAddress ?? obj.PayerReceiverAddress;
            obj.DocumentNo           = model.DocumentNo           ?? obj.DocumentNo;
            obj.Amount               = model.Amount;
            obj.Description          = model.Description;
            obj.IsAdjustment         = model.IsAdjustment;
            obj.PaymentMethod        = model.PaymentMethod;
            obj.BankName             = model.BankName;
            obj.BankAccountNumber    = model.BankAccountNumber;

            await DbContext.SaveChangesAsync();
            return obj;
        }

        /// <summary>
        /// Mapping phiếu thu dạng Save() -> CollectLoanPaymentModel.
        /// Chỉ apply cho các reason code liên quan thu nợ để tránh ảnh hưởng phiếu thu khác.
        /// </summary>
        private static bool TryBuildCollectLoanPaymentModel(CUCashVoucherModel model, out CollectLoanPaymentModel? collectModel)
        {
            collectModel = null;

            if (model.IsAdjustment) return false;
            if (!string.Equals(model.VoucherType, VoucherType.Receipt, StringComparison.OrdinalIgnoreCase)) return false;
            if (!model.LoanContractId.HasValue || model.LoanContractId == Guid.Empty) return false;
            if (model.Amount <= 0) return false;

            var reason = (model.ReasonCode ?? string.Empty).Trim().ToUpperInvariant();
            var purposes = reason switch
            {
                "LOAN_COLLECTION" or "INSTALLMENT_COLLECTION" or "BAD_DEBT_RECOVERY"
                    => new List<string> { "INTEREST", "QLKV_FEE", "QLTS_FEE", "LATE_PENALTY", "PRINCIPAL" },

                "UPFRONT_FEE_COLLECTION"
                    => new List<string> { "FILE_FEE", "INSURANCE" },

                "FILE_FEE"
                    => new List<string> { "FILE_FEE" },

                "INSURANCE"
                    => new List<string> { "INSURANCE" },

                "LATE_PENALTY"
                    => new List<string> { "LATE_PENALTY" },

                _ => new List<string>(),
            };

            if (purposes.Count == 0) return false;

            collectModel = new CollectLoanPaymentModel
            {
                LoanContractId = model.LoanContractId.Value,
                Purposes = purposes,
                ReasonCode = model.ReasonCode,
                Amount = model.Amount,
                PayerReceiverName = model.PayerReceiverName,
                Description = model.Description,
            };

            return true;
        }

        public async Task<IList<object>> GetByLoanContract(Guid loanContractId)
        {
            return await DbContext.CashVouchers
                .Where(v => v.LoanContractId == loanContractId)
                .OrderByDescending(v => v.VoucherDatetime)
                .Select(v => (object)new
                {
                    v.VoucherId,
                    v.VoucherNo,
                    v.VoucherType,
                    v.ReasonCode,
                    v.BusinessDate,
                    v.VoucherDatetime,
                    v.Amount,
                    v.Description,
                    v.IsAdjustment,
                    v.PayerReceiverName,
                    CreatedByName = v.CreatedByNavigation != null ? v.CreatedByNavigation.FullName : null,
                    Allocations = v.CashVoucherAllocations.Select(a => new
                    {
                        a.ComponentCode,
                        a.Amount,
                        a.ScheduleId,
                    }).ToList(),
                })
                .ToListAsync();
        }

        // -----------------------------------------------------------------------------
        // CollectLoanPayment - thu tiền khoản vay, phân bổ đúng kỳ/phí
        // Thứ tự ưu tiên: Lãi -> Phí định kỳ -> Phạt chậm -> Gốc -> Phí hồ sơ/BH -> Dư
        // -----------------------------------------------------------------------------
        public async Task<CashVoucher> CollectLoanPayment(CollectLoanPaymentModel model)
        {
            // 1. Validate hợp đồng
            var contract = await DbContext.LoanContracts
                .Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.LoanContractId == model.LoanContractId)
                ?? throw new KeyNotFoundException($"Không tìm thấy hợp đồng Id = {model.LoanContractId}");

            bool isUpfrontFeeOnly = model.Purposes.All(p => p == "FILE_FEE" || p == "INSURANCE");
            bool canCollect = contract.StatusCode == "DISBURSED"
                           || contract.StatusCode == "BAD_DEBT"
                           || (isUpfrontFeeOnly && contract.StatusCode == "PENDING_DISBURSEMENT");
            if (!canCollect)
                throw new InvalidOperationException($"Không thể thu tiền khi hợp đồng ở trạng thái '{contract.StatusCode}'.");

            decimal remaining = model.Amount;
            var allocations   = new List<CashVoucherAllocation>();
            var purposes      = model.Purposes.ToHashSet();

            // 2. Lấy tất cả kỳ chưa thanh toán, sau đó phân bổ tiền theo thứ tự từ kỳ hiện tại sang các kỳ sau.
            var unpaidSchedules = await DbContext.LoanRepaymentSchedules
                .Where(s => s.LoanContractId == model.LoanContractId && s.StatusCode != "PAID")
                .OrderBy(s => s.DueDate)
                .ThenBy(s => s.PeriodNo)
                .ToListAsync();

            // 3. Thu các khoản theo thứ tự ưu tiên, lan sang các kỳ sau nếu còn dư.
            if (unpaidSchedules.Count > 0)
            {
                // allocComponent = giá trị ghi vào CashVoucherAllocation.ComponentCode (phải khớp DB check constraint)
                // scheduleComponent = mã nội bộ dùng trong Purposes để cho phép/bỏ qua bước
                (string allocComponent, string scheduleComponent, Func<LoanRepaymentSchedule, decimal> due, Action<LoanRepaymentSchedule, decimal> apply)[] steps =
                [
                    (VoucherComponentCode.Interest,    "INTEREST",     s => s.DueInterestAmount     - s.PaidInterestAmount,     (s, v) => s.PaidInterestAmount     += v),
                    (VoucherComponentCode.PeriodicFee, "QLKV_FEE",    s => s.DueQlkvAmount         - s.PaidQlkvAmount,         (s, v) => { s.PaidQlkvAmount += v; s.PaidPeriodicFeeAmount += v; }),
                    (VoucherComponentCode.PeriodicFee, "QLTS_FEE",    s => s.DueQltsAmount         - s.PaidQltsAmount,         (s, v) => { s.PaidQltsAmount += v; s.PaidPeriodicFeeAmount += v; }),
                    (VoucherComponentCode.LatePenalty,  "LATE_PENALTY", s => s.DueLatePenaltyAmount  - s.PaidLatePenaltyAmount,  (s, v) => s.PaidLatePenaltyAmount  += v),
                    (VoucherComponentCode.Principal,   "PRINCIPAL",    s => s.DuePrincipalAmount     - s.PaidPrincipalAmount,    (s, v) => s.PaidPrincipalAmount    += v),
                ];

                foreach (var schedule in unpaidSchedules)
                {
                    if (remaining <= 0) break;

                    foreach (var (allocComponent, scheduleComponent, dueFunc, applyFunc) in steps)
                    {
                        if (!purposes.Contains(scheduleComponent) || remaining <= 0) continue;
                        decimal due = Math.Max(0, dueFunc(schedule));
                        decimal take = Math.Min(remaining, due);
                        if (take <= 0) continue;

                        applyFunc(schedule, take);
                        remaining -= take;
                        allocations.Add(new CashVoucherAllocation
                        {
                            AllocationId = Guid.CreateVersion7(),
                            LoanContractId = model.LoanContractId,
                            ScheduleId = schedule.ScheduleId,
                            ComponentCode = allocComponent,
                            Amount = take,
                        });
                    }

                    UpdateScheduleStatus(schedule);
                }
            }

            // 4. Phí LoanCharge (FILE_FEE, INSURANCE)
            // Nếu chưa có dòng LoanCharge thì tự tạo từ snapshot trên hợp đồng
            foreach (var chargeCode in new[] { "FILE_FEE", "INSURANCE" })
            {
                if (!purposes.Contains(chargeCode) || remaining <= 0) continue;

                var charge = await DbContext.LoanCharges
                    .FirstOrDefaultAsync(c => c.LoanContractId == model.LoanContractId && c.ChargeCode == chargeCode);

                if (charge == null)
                {
                    // Xác định số tiền từ snapshot hợp đồng
                    decimal snapshotAmount = chargeCode == "FILE_FEE"
                        ? contract.FileFeeAmountSnapshot
                        : contract.InsuranceAmountSnapshot;

                    if (snapshotAmount <= 0) continue; // Hợp đồng không có khoản phí này -> bỏ qua

                    charge = new LoanCharge
                    {
                        ChargeId       = Guid.CreateVersion7(),
                        LoanContractId = model.LoanContractId,
                        ChargeCode     = chargeCode,
                        ChargeName     = chargeCode == "FILE_FEE" ? "Phí hồ sơ" : "Bảo hiểm",
                        Amount         = snapshotAmount,
                        PaidAmount     = 0,
                        StatusCode     = "PENDING",
                        CreatedBy      = CommonLib.GetGUID(User.UserId),
                        CreatedAt      = DateTime.Now,
                    };
                    await DbContext.LoanCharges.AddAsync(charge);
                }

                decimal due  = Math.Max(0, charge.Amount - charge.PaidAmount);
                decimal take = Math.Min(remaining, due);
                if (take <= 0) continue;

                charge.PaidAmount += take;
                if (charge.PaidAmount >= charge.Amount) charge.StatusCode = "PAID";
                remaining -= take;
                allocations.Add(new CashVoucherAllocation
                {
                    AllocationId   = Guid.CreateVersion7(),
                    LoanContractId = model.LoanContractId,
                    ChargeId       = charge.ChargeId,
                    ComponentCode  = chargeCode,
                    Amount         = take,
                });
            }

            // 5. Tiền dư -> OTHER_INCOME
            if (remaining > 0)
            {
                allocations.Add(new CashVoucherAllocation
                {
                    AllocationId   = Guid.CreateVersion7(),
                    LoanContractId = model.LoanContractId,
                    ComponentCode  = "OTHER_INCOME",
                    Amount         = remaining,
                });
            }

            // 6. Tạo CashVoucher + gắn allocations
            var voucher = new CashVoucher
            {
                VoucherId         = Guid.CreateVersion7(),
                VoucherNo         = $"PT-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                StoreId           = contract.StoreId,
                VoucherType       = "RECEIPT",
                ReasonCode        = model.ReasonCode,
                BusinessDate      = DateOnly.FromDateTime(DateTime.Today),
                VoucherDatetime   = DateTime.Now,
                CustomerId        = contract.CustomerId,
                LoanContractId    = model.LoanContractId,
                PayerReceiverName = model.PayerReceiverName ?? contract.Customer?.FullName,
                Amount            = model.Amount,
                Description       = model.Description,
                IsAdjustment      = false,
                CreatedBy         = CommonLib.GetGUID(User.UserId),
            };
            foreach (var alloc in allocations)
                alloc.VoucherId = voucher.VoucherId;

            DbContext.CashVouchers.Add(voucher);
            DbContext.CashVoucherAllocations.AddRange(allocations);
            await DbContext.SaveChangesAsync();
            return voucher;
        }

        /// <summary>Cập nhật StatusCode kỳ dựa trên số đã trả.</summary>
        private static void UpdateScheduleStatus(LoanRepaymentSchedule s)
        {
            // Sync PaidPeriodicFeeAmount with Qlkv and Qlts to ensure backward compatibility and total calculation
            s.PaidPeriodicFeeAmount = s.PaidQlkvAmount + s.PaidQltsAmount;

            bool allPaid = s.PaidPrincipalAmount >= s.DuePrincipalAmount
                        && s.PaidInterestAmount  >= s.DueInterestAmount
                        && s.PaidPeriodicFeeAmount >= s.DuePeriodicFeeAmount
                        && s.PaidLatePenaltyAmount >= s.DueLatePenaltyAmount;
            bool anyPaid = (s.PaidPrincipalAmount + s.PaidInterestAmount
                          + s.PaidPeriodicFeeAmount + s.PaidLatePenaltyAmount) > 0;

            if (allPaid)        { s.StatusCode = "PAID"; s.FullyPaidAt ??= DateTime.Now; }
            else if (anyPaid)   { s.StatusCode = s.DueDate < DateOnly.FromDateTime(DateTime.Today) ? "OVERDUE" : "PARTIAL"; }
        }

        public async Task<object> SearchCashVoucher(string keyword, int pageIndex, int pageSize, string? sortBy, bool sortDesc, Guid? loanContractId = null)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;


            var query = DbContext.CashVouchers.AsQueryable();

            var storeScopeIds = GetStoreScopeIds();
            if (storeScopeIds is not null)
                query = query.Where(v => storeScopeIds.Contains(v.StoreId));

            if (loanContractId.HasValue)
                query = query.Where(v => v.LoanContractId == loanContractId.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(v =>
                    v.VoucherNo.ToLower().Contains(keyword) ||
                    v.Description.ToLower().Contains(keyword) ||
                    (v.PayerReceiverName != null && v.PayerReceiverName.ToLower().Contains(keyword)));
            }

            int total = await query.CountAsync();

            var sorted = (sortBy?.Trim().ToLower() ?? "voucherdatetime") switch
            {
                "voucherno"       => sortDesc ? query.OrderByDescending(v => v.VoucherNo)       : query.OrderBy(v => v.VoucherNo),
                "businessdate"    => sortDesc ? query.OrderByDescending(v => v.BusinessDate)    : query.OrderBy(v => v.BusinessDate),
                "vouchertype"     => sortDesc ? query.OrderByDescending(v => v.VoucherType)     : query.OrderBy(v => v.VoucherType),
                "amount"          => sortDesc ? query.OrderByDescending(v => v.Amount)          : query.OrderBy(v => v.Amount),
                "createdat"       => sortDesc ? query.OrderByDescending(v => v.CreatedAt)       : query.OrderBy(v => v.CreatedAt),
                _                 => sortDesc ? query.OrderByDescending(v => v.VoucherDatetime) : query.OrderBy(v => v.VoucherDatetime)
            };

            var items = await sorted
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new
                {
                    v.VoucherId,
                    v.VoucherNo,
                    v.VoucherType,
                    v.ReasonCode,
                    v.BusinessDate,
                    v.VoucherDatetime,
                    v.Amount,
                    v.Description,
                    v.IsAdjustment,
                    v.PayerReceiverName,
                    v.LoanContractId,
                    ContractNo = v.LoanContract != null ? v.LoanContract.ContractNo : null,
                    v.CreatedAt,
                    v.CreatedBy,
                    v.PaymentMethod,
                    v.BankName,
                    v.BankAccountNumber,
                })
                .ToArrayAsync();
            return new { Total = total, PageIndex = pageIndex, PageSize = pageSize, Items = items };
        }
    }
}

