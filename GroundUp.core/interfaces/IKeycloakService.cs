using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroundUp.core.interfaces
{
    public interface IKeycloakService
    {
        Task<bool> ValidateTokenAsync(string token);
        // Add more methods as needed
    }
}
