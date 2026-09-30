using GlobalPay.Application.Abstractions.Validation;

namespace GlobalPay.Api.Endpoints;

internal sealed class ValidationEndpointFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var validator = context.HttpContext.RequestServices.GetService<IRequestValidator<TRequest>>();

        if (validator is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        var validation = validator.Validate(request);

        if (validation.IsValid)
        {
            return await next(context).ConfigureAwait(false);
        }

        var errors = validation.Errors
            .GroupBy(error => error.Field)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => $"{error.Code}: {error.Message}").ToArray());

        return Results.ValidationProblem(
            errors,
            title: "One or more validation errors occurred.",
            extensions: new Dictionary<string, object?> { ["code"] = "request.validation_failed" });
    }
}
