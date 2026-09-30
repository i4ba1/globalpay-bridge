namespace GlobalPay.Application.Abstractions.Messaging;

public interface ICommand<TResponse> : IRequest<TResponse>
{
}

public interface ICommand : ICommand<Unit>
{
}
