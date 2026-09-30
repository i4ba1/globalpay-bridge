using Autofac;
using Autofac.Extensions.DependencyInjection;
using GlobalPay.Api.Composition;
using GlobalPay.Api.Endpoints;
using GlobalPay.Api.Middleware;
using GlobalPay.Application;
using GlobalPay.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container =>
    container.RegisterModule<GlobalPayApiModule>());

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });

app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "GlobalPay Bridge API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "GlobalPay Bridge API";
    options.DisplayRequestDuration();
    options.EnablePersistAuthorization();
});

app.MapScalarApiReference();

app.MapWorkerEndpoints();
app.MapTimesheetImportEndpoints();
app.MapPayrollEndpoints();
app.MapPaymentEndpoints();
app.MapWebhookEndpoints();
app.MapReconciliationEndpoints();
app.MapAuditEndpoints();

app.MapGet("/", () => Results.Ok(new { service = "GlobalPay.Api", status = "healthy" }))
    .WithTags("Diagnostics")
    .WithName("GetServiceStatus")
    .WithSummary("Service status");

await app.RunAsync();

public partial class Program;
