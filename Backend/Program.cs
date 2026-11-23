using System;
using Backend.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Backend.Audit;

namespace Backend
{
    internal static class Program
    {
        private static void Main(string[] args)
    {
        using (IHost host = BuildHost(args))
        {
            IMessageQueue queue = host.Services.GetRequiredService<IMessageQueue>();

            Console.WriteLine("Backend console app is running.");
        Console.WriteLine($"Selected queue: {queue.GetType().Name}");
        RunSendDemo(queue);
        }
    }

    internal static IHost BuildHost(string[] args)
    {
        HostApplicationBuilderSettings settings = new HostApplicationBuilderSettings();
        settings.Args = args;
        settings.ContentRootPath = AppContext.BaseDirectory;

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);

        builder.Services.AddSingleton<InMemoryMessageQueue>();
        builder.Services.AddSingleton<DatqMessageQueue>(CreateDatqQueue);
        builder.Services.AddSingleton<IMessageQueue>(SelectQueue);

        return builder.Build();
    }

        private static IMessageQueue SelectQueue(IServiceProvider services)
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            string? queueType = configuration["Queue:Type"];

            if (queueType == "Sim")
            {
                return services.GetRequiredService<InMemoryMessageQueue>();
            }

            if (queueType == "Datq")
            {
                return services.GetRequiredService<DatqMessageQueue>();
            }

            throw new InvalidOperationException(
                "Set Queue:Type to Sim or Datq in appsettings.json.");
        }

        private static DatqMessageQueue CreateDatqQueue(IServiceProvider services)
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();

            string? server = configuration["Queue:Datq:Server"];
            string? password = configuration["Queue:Datq:Password"];

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Set Queue:Datq:Server and Queue:Datq:Password for the DATQ simulator.");
            }

            return new DatqMessageQueue(server, password);
        }
        private static void RunSendDemo(IMessageQueue queue)
        {
            ExchangeSession session = new ExchangeSession();

            if (!session.TryTransitionTo(SessionState.Connecting) ||
                !session.TryTransitionTo(SessionState.LoggingIn) ||
                !session.TryTransitionTo(SessionState.Active))
            {
                throw new InvalidOperationException("The demo session could not become active.");
            }

            InMemoryAuditLog auditLog = new InMemoryAuditLog();
            ExchangeMessageSender sender = new ExchangeMessageSender(session, queue, auditLog);

            bool sent = sender.TrySend("Demo message");

            Console.WriteLine($"Sent: {sent}");
            Console.WriteLine($"Audit entries: {auditLog.Entries.Count}");
        }
    }
}