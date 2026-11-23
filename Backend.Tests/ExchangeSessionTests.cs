using Xunit;

namespace Backend.Tests
{
    public class ExchangeSessionTests
    {
        [Fact]
        public void DisconnectedSessionCannotTransitionToActive()
        {
            ExchangeSession session = new ExchangeSession();

            bool transitioned = session.TryTransitionTo(SessionState.Active);

            Assert.False(transitioned);
            Assert.Equal(SessionState.Disconnected, session.State);
        }

        [Fact]
        public void SuspendedSessionCanTransitionToActive()
        {
            ExchangeSession session = new ExchangeSession();

            Assert.True(session.TryTransitionTo(SessionState.Connecting));
            Assert.True(session.TryTransitionTo(SessionState.LoggingIn));
            Assert.True(session.TryTransitionTo(SessionState.Active));
            Assert.True(session.TryTransitionTo(SessionState.Suspended));
            Assert.Equal(SessionState.Suspended, session.State);

            bool transitioned = session.TryTransitionTo(SessionState.Active);

            Assert.True(transitioned);
            Assert.Equal(SessionState.Active, session.State);
        }

        [Fact]
        public void ActiveSessionCanTransitionToReconnecting()
        {
            ExchangeSession session = new ExchangeSession();

            Assert.True(session.TryTransitionTo(SessionState.Connecting));
            Assert.True(session.TryTransitionTo(SessionState.LoggingIn));
            Assert.True(session.TryTransitionTo(SessionState.Active));

            bool transitioned = session.TryTransitionTo(SessionState.Reconnecting);

            Assert.True(transitioned);
            Assert.Equal(SessionState.Reconnecting, session.State);
        }

        [Fact]
        public void ReconnectingSessionCannotTransitionDirectlyToActive()
        {
            ExchangeSession session = new ExchangeSession();

            Assert.True(session.TryTransitionTo(SessionState.Connecting));
            Assert.True(session.TryTransitionTo(SessionState.LoggingIn));
            Assert.True(session.TryTransitionTo(SessionState.Active));
            Assert.True(session.TryTransitionTo(SessionState.Reconnecting));

            bool transitioned = session.TryTransitionTo(SessionState.Active);

            Assert.False(transitioned);
            Assert.Equal(SessionState.Reconnecting, session.State);
        }
    }

}
