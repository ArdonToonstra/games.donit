// Blazor Server boot + reconnect tuning.
//
// Why this file exists at all: on a party-game site every player's phone locks
// its screen, which drops the WebSocket. Blazor's stock reconnect gives up
// after ~8 attempts over ~30 seconds — far too soon for a phone that has been
// in someone's pocket for two minutes.
Blazor.start({
  circuit: {
    reconnectionOptions: {
      maxRetries: 60,
      retryIntervalMilliseconds: (attempt) =>
        attempt < 5 ? 1000 : Math.min(1000 * 2 ** (attempt - 4), 15000)
    }
  }
});

// Reconnect the instant the phone wakes, instead of waiting for the next
// scheduled retry. Without this an unlocked phone can sit on the reconnect
// modal for many seconds even though the network is already back.
document.addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'visible') {
    try { Blazor.reconnect?.(); } catch { /* not disconnected — nothing to do */ }
  }
});
