using GroundUp.Core.interfaces;
using Serilog;

namespace GroundUp.Services.Core.Services;

public sealed class LoggingService : ILoggingService
{
    public void LogInformation(string message) => Log.Information(message);

    public void LogWarning(string message) => Log.Warning(message);

    public void LogError(string message, Exception? ex = null) => Log.Error(ex, message);
}
