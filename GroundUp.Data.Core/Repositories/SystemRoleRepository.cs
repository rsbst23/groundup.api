using AutoMapper;
using GroundUp.Data.Abstractions.Interfaces;
using GroundUp.core.interfaces;
using GroundUp.Data.Core.Data;

namespace GroundUp.Data.Core.Repositories;

// NOTE: Interface currently has no members; this is a placeholder implementation
// to keep DI and layering consistent while migration completes.
public class SystemRoleRepository : ISystemRoleRepository
{
    public SystemRoleRepository(ApplicationDbContext context, IMapper mapper, ILoggingService logger)
    {
        // Intentionally unused until ISystemRoleRepository is re-enabled.
    }
}
