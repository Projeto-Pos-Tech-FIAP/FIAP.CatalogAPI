using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FIAP.CatalogAPI.Api.Filters;

// Marca no Swagger, por operação, quais endpoints exigem [Authorize] — sem isso o botão
// "Authorize" do Swagger UI guarda o token mas não o anexa nas chamadas, porque nenhuma
// operação fica marcada como exigindo o esquema "Bearer".
public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true
            || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        if (!hasAuthorize)
            return;

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                // O segundo parâmetro (o OpenApiDocument sendo montado) é obrigatório no
                // Microsoft.OpenApi 2.x — sem ele a referência não resolve e vira objeto vazio.
                { new OpenApiSecuritySchemeReference("Bearer", context.Document), new List<string>() }
            }
        };
    }
}
