using CrediFlow.Common.Services;

namespace CrediFlow.API.Extensions;

public static class UserInfoServiceExtensions
{
    public static List<Guid>? GetStoreScopeIds(this IUserInfoService user, Guid? requestedStoreId = null)
    {
        if (user.IsAdmin)
            return requestedStoreId.HasValue ? new List<Guid> { requestedStoreId.Value } : null;

        var scopeIds = user.IsRegionalManager
            ? user.AssignedStoreIds.ToList()
            : user.StoreId.HasValue ? new List<Guid> { user.StoreId.Value } : new List<Guid>();

        if (requestedStoreId.HasValue)
            return scopeIds.Any(id => id == requestedStoreId.Value) ? new List<Guid> { requestedStoreId.Value } : new List<Guid>();

        return scopeIds;
    }
}
