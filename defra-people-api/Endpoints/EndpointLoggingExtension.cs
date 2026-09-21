using Defra_People_API.Services.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Defra_People_API.Endpoints;

public static class EndpointLoggingExtension
{
    public static RouteHandlerBuilder WithApiLogging(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            
            var httpContext = context.HttpContext;
            var loggerFilter = httpContext.RequestServices.GetRequiredService<ApiLoggerFilter>();
            
            var actionContext = new ActionContext
            {
                HttpContext = httpContext,
                RouteData = httpContext.GetRouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            };
                        
            var objectResult = new ObjectResult(result)
            {
                StatusCode = result.GetType().GetProperty("StatusCode")?.GetValue(result) as int? ?? StatusCodes.Status200OK,
            };
            
            var resultExecutingContext = new ResultExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                objectResult,
                null!);
            
            await loggerFilter.OnResultExecutionAsync(
                resultExecutingContext,
                () => Task.FromResult(new ResultExecutedContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    objectResult,
                    null!)));
            
            return result;
        });
    }
}
