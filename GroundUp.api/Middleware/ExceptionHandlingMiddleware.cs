using GroundUp.core;
using GroundUp.core.dtos;
using Serilog;
using System.Net;
using System.Text.Json;

namespace GroundUp.api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context); // Continue to the next middleware
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unhandled exception occurred.");

                context.Response.ContentType = "application/json";
                ApiResponse<string> response;

                // Handle permission/authorization related errors
                if (ex.Message.Contains("lacks permission"))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    response = new ApiResponse<string>(
                        string.Empty,
                        false,
                        "You do not have permission to access this resource.",
                        new List<string> { ex.Message },
                        StatusCodes.Status403Forbidden,
                        ErrorCodes.Forbidden);
                }
                // Handle other types of errors
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response = new ApiResponse<string>(
                        string.Empty,
                        false,
                        "An unexpected error occurred. Please try again later.",
                        new List<string> { ex.Message },
                        StatusCodes.Status500InternalServerError,
                        ErrorCodes.UnhandledException);
                }

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}
