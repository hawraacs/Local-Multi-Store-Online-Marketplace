using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Local_Multi_Store_Online_Marketplace.Data;

public class ApplicationDbContext_old : IdentityDbContext
{
    public ApplicationDbContext_old(DbContextOptions<ApplicationDbContext_old> options)
        : base(options)
    {
    }
}
