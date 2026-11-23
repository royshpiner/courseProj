using Backend.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Backend.Tests
{
    public class QueueSelectionTests
    {
        [Fact]
        public void SimConfigurationSelectsInMemoryQueue()
        {
            string[] args = new string[] { "--Queue:Type=Sim" };

            using (IHost host = Program.BuildHost(args))
            {
                IMessageQueue queue =
                    host.Services.GetRequiredService<IMessageQueue>();

                Assert.IsType<InMemoryMessageQueue>(queue);
            }
        }
    }
}
