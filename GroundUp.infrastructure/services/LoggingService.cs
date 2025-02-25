using GroundUp.core.interfaces;
using Serilog;

namespace GroundUp.infrastructure.services
{
    public class LoggingService : ILoggingService
    {
        public void LogInformation(string message)
        {
            Log.Information(message);
        }

        public void LogWarning(string message)
        {
            Log.Warning(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            Log.Error(ex, message);
        }
    }
}
