using System;
using System.Collections.Generic;
using System.IO;
using Backend.Audit;
using Backend.Persistence;
using Backend.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            using IHost host = BuildHost(args);
            RunSendDemo(host.Services);
        }

        internal static IHost BuildHost(string[] args)
        {
            HostApplicationBuilderSettings settings = new HostApplicationBuilderSettings();
            settings.Args = args;
            settings.ContentRootPath = AppContext.BaseDirectory;

            HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);

            builder.Services.AddSingleton<DirectoryInfo>(CreateStorageDirectory);
            builder.Services.AddSingleton<InMemoryMessageQueue>();
            builder.Services.AddSingleton<DatqMessageQueue>(CreateDatqQueue);
            builder.Services.AddSingleton<SqliteMessageQueue>(CreateSqliteQueue);
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

            if (queueType == "DurableSim")
            {
                return services.GetRequiredService<SqliteMessageQueue>();
            }

            throw new InvalidOperationException(
                "Set Queue:Type to Sim, Datq, or DurableSim in appsettings.json.");
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

        private static DirectoryInfo CreateStorageDirectory(IServiceProvider services)
        {
            IConfiguration configuration = services.GetRequiredService<IConfiguration>();
            string? dataFolder = configuration["Storage:Directory"];

            if (string.IsNullOrWhiteSpace(dataFolder))
            {
                dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "courseProj");
            }

            return Directory.CreateDirectory(dataFolder);
        }

        private static SqliteMessageQueue CreateSqliteQueue(IServiceProvider services)
        {
            DirectoryInfo storageDirectory = services.GetRequiredService<DirectoryInfo>();
            string receiptPath = Path.Combine(storageDirectory.FullName, "DurableSim-receipts.sqlite");
            return new SqliteMessageQueue(receiptPath);
        }

        internal static void RunSendDemo(IServiceProvider services)
        {
            IMessageQueue queue = services.GetRequiredService<IMessageQueue>();
            DirectoryInfo storageDirectory = services.GetRequiredService<DirectoryInfo>();

            Console.WriteLine("Backend console app is running.");
            Console.WriteLine($"Selected queue: {queue.GetType().Name}");

            bool supportsAutomaticRecovery = queue is SqliteMessageQueue;
            string databaseName = supportsAutomaticRecovery ? "DurableSim-outbox.sqlite" : $"{queue.GetType().Name}.sqlite";
            string databasePath = Path.Combine(storageDirectory.FullName, databaseName);
            SqliteOutbox outbox = new SqliteOutbox(databasePath);
            Console.WriteLine($"Outbox database at {databasePath}");

            if (!supportsAutomaticRecovery && ReportUnconfirmedSends(outbox))
            {
                return;
            }

            ExchangeSession session = new ExchangeSession();

            if (!session.TryTransitionTo(SessionState.Connecting) ||
                !session.TryTransitionTo(SessionState.LoggingIn) ||
                !session.TryTransitionTo(SessionState.Active))
            {
                throw new InvalidOperationException("The demo session could not become active.");
            }

            InMemoryAuditLog auditLog = new InMemoryAuditLog();
            ExchangeMessageSender sender = new ExchangeMessageSender(session, queue, auditLog, outbox);

            if (supportsAutomaticRecovery)
            {
                int recoveredCount = sender.RecoverUnconfirmedSends();
                if (recoveredCount > 0)
                {
                    Console.WriteLine($"Recovered {recoveredCount} unconfirmed send(s).");
                    Console.WriteLine($"Audit entries: {auditLog.Entries.Count}");
                    return;
                }
            }

            bool sent = sender.TrySend("Demo message");

            Console.WriteLine($"Sent: {sent}");
            Console.WriteLine($"Audit entries: {auditLog.Entries.Count}");
        }

        private static bool ReportUnconfirmedSends(SqliteOutbox outbox)
        {
            IReadOnlyList<Guid> unconfirmedIds = outbox.GetUnconfirmedMessageIds();
            if (unconfirmedIds.Count == 0)
            {
                return false;
            }

            Console.Error.WriteLine("Check these unconfirmed sends before continuing.");
            foreach (Guid messageId in unconfirmedIds)
            {
                Console.Error.WriteLine(messageId);
            }

            return true;
        }
    }
}
