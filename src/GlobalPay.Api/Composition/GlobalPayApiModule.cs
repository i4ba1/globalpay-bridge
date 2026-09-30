using Autofac;
using GlobalPay.Api.Identity;
using GlobalPay.Application.Abstractions;

namespace GlobalPay.Api.Composition;

internal sealed class GlobalPayApiModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.RegisterType<HttpCurrentUser>().As<ICurrentUser>().InstancePerLifetimeScope();
    }
}
