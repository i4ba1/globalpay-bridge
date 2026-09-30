using System.Reflection;
using GlobalPay.Application;
using GlobalPay.Domain.Common;
using GlobalPay.Infrastructure;
using NetArchTest.Rules;

namespace GlobalPay.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity<>).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(ApplicationServiceCollectionExtensions).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(InfrastructureServiceCollectionExtensions).Assembly;

    private static readonly Assembly ApiAssembly = Assembly.Load("GlobalPay.Api");

    private static readonly Assembly FunctionsAssembly = Assembly.Load("GlobalPay.Functions");

    private static readonly Assembly ContractsAssembly = Assembly.Load("GlobalPay.Contracts");

    [Fact]
    public void Domain_Should_Not_Depend_On_Any_Outer_Layer()
    {
        AssertNoDependency(
            DomainAssembly,
            "GlobalPay.Application",
            "GlobalPay.Infrastructure",
            "GlobalPay.Api",
            "GlobalPay.Functions",
            "GlobalPay.Contracts");
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Frameworks()
    {
        AssertNoDependency(
            DomainAssembly,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Azure.",
            "Autofac",
            "Dapper",
            "Serilog");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Hosts()
    {
        AssertNoDependency(
            ApplicationAssembly,
            "GlobalPay.Infrastructure",
            "GlobalPay.Api",
            "GlobalPay.Functions",
            "Microsoft.EntityFrameworkCore",
            "Azure.");
    }

    [Fact]
    public void Contracts_Should_Only_Depend_On_Primitives()
    {
        AssertNoDependency(
            ContractsAssembly,
            "GlobalPay.Domain",
            "GlobalPay.Application",
            "GlobalPay.Infrastructure",
            "GlobalPay.Api",
            "GlobalPay.Functions");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Hosts()
    {
        AssertNoDependency(InfrastructureAssembly, "GlobalPay.Api", "GlobalPay.Functions");
    }

    [Fact]
    public void Api_Should_Not_Be_Referenced_By_Functions()
    {
        AssertNoDependency(FunctionsAssembly, "GlobalPay.Api");
    }

    [Fact]
    public void ApiAssembly_Should_Exist()
    {
        Assert.NotNull(ApiAssembly.GetName().Name);
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbiddenDependencies)
    {
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} has forbidden dependencies via: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
