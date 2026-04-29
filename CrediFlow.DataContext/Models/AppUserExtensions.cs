using System.ComponentModel.DataAnnotations.Schema;

namespace CrediFlow.DataContext.Models;

public partial class AppUser
{
    [NotMapped]
    public List<Guid>? StoreIds { get; set; }
}
