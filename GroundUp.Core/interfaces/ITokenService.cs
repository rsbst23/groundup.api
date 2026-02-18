using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GroundUp.Core.interfaces
{
    public interface ITokenService
    {
        Task<string> GenerateTokenAsync(Guid userId, int tenantId, IEnumerable<Claim> existingClaims);
    }
}
