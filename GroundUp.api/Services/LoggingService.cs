using Serilog;

namespace GroundUp.api.Services
{
    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex);
    }

    public class LoggingService : ILoggingService
    {
        public void LogInfo(string message) => Log.Information(message);
        public void LogWarning(string message) => Log.Warning(message);
        public void LogError(string message, Exception ex) => Log.Error(ex, message);
    }
}
