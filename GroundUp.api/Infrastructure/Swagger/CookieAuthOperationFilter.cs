using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GroundUp.api.Infrastructure.Swagger
{
    public class CookieAuthOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "AuthToken",
                In = ParameterLocation.Cookie,
                Required = false
            });
        }
    }
}
