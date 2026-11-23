using System.Collections.Generic;

namespace Backend
{
    public sealed class ExchangeSession
    {
        private static readonly HashSet<(SessionState From, SessionState To)> AllowedTransitions = CreateAllowedTransitions();

        public SessionState State { get; private set; }


        public ExchangeSession()
        {
            State = SessionState.Disconnected;
        }

        public bool TryTransitionTo(SessionState nextState)
        {
            bool isAllowed = AllowedTransitions.Contains((State, nextState));

            if (!isAllowed)
            {
                return false;
            }

            State = nextState;
            return true;
        }

        private static HashSet<(SessionState From, SessionState To)>CreateAllowedTransitions()
        {
            HashSet<(SessionState From, SessionState To)> transitions = new HashSet<(SessionState From, SessionState To)>();

            transitions.Add((SessionState.Disconnected, SessionState.Connecting));

            transitions.Add((SessionState.Connecting, SessionState.LoggingIn));
            transitions.Add((SessionState.Connecting, SessionState.Disconnected));

            transitions.Add((SessionState.LoggingIn, SessionState.Active));
            transitions.Add((SessionState.LoggingIn, SessionState.Disconnected));

            transitions.Add((SessionState.Active, SessionState.Suspended));
            transitions.Add((SessionState.Active, SessionState.LoggingOut));
            transitions.Add((SessionState.Active, SessionState.Reconnecting));

            transitions.Add((SessionState.Suspended, SessionState.Active));
            transitions.Add((SessionState.Suspended, SessionState.LoggingOut));
            transitions.Add((SessionState.Suspended, SessionState.Reconnecting));


            transitions.Add((SessionState.LoggingOut, SessionState.Disconnected));


            transitions.Add((SessionState.Reconnecting, SessionState.LoggingIn));
            transitions.Add((SessionState.Reconnecting, SessionState.Disconnected));

            return transitions;
        }
    }
}