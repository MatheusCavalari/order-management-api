using Application.Events;
using Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Events;

public class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessDomainEventDispatcher> _logger;

    public InProcessDomainEventDispatcher(IServiceProvider serviceProvider, ILogger<InProcessDomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        foreach (var domainEvent in events)
        {
            Type handlerType;
            IEnumerable<object> handlers;

            try
            {
                handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
                handlers = (IEnumerable<object>)_serviceProvider.GetServices(handlerType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve domain event handlers for event {EventType}", domainEvent.GetType().Name);
                continue;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    var handleMethod = handlerType.GetMethod("HandleAsync")!;
                    await (Task)handleMethod.Invoke(handler, new object[] { domainEvent })!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Domain event handler {HandlerType} failed for event {EventType}", handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}
