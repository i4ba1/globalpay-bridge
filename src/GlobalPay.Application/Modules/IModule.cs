using Microsoft.Extensions.DependencyInjection;

namespace GlobalPay.Application.Modules;

public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services);
}
