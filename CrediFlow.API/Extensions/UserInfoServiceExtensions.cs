using CrediFlow.Common.Services;

namespace CrediFlow.API.Extensions;

public static class UserInfoServiceExtensions
{
    public static IReadOnlyList<Guid>? GetStoreScopeIds(this IUserInfoService user, Guid? requestedStoreId = null)
    {
        if (user.IsAdmin)
            return requestedStoreId.HasValue ? [requestedStoreId.Value] : null;

        var scopeIds = user.IsRegionalManager
            ? user.AssignedStoreIds.ToList()
            : user.StoreId.HasValue ? [user.StoreId.Value] : [];

        if (requestedStoreId.HasValue)
            return scopeIds.Any(id => id == requestedStoreId.Value) ? [requestedStoreId.Value] : [];

        return scopeIds;
    }
}
