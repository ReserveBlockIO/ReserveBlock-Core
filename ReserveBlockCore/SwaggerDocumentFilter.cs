using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ReserveBlockCore
{
    public class SwaggerDocumentFilter<T> : IDocumentFilter where T : class
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var DocumentNames = typeof(T).GetCustomAttribute<ApiExplorerSettingsAttribute>();
            if (DocumentNames == null || !DocumentNames.GroupName.Any() || context.DocumentName == DocumentNames.GroupName)
            {
                context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
            }
        }
    }
}
