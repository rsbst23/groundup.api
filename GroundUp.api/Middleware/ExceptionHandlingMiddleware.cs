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
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var response = new ApiResponse<string>(null, false, "An unexpected error occurred. Please try again later.", new List<string> { ex.Message });

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}
