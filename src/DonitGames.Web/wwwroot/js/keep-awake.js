// Screen Wake Lock, for the phases where a player is only watching.
//
// Just One leaves you idle for a full minute at a time — waiting for four other
// people to type a clue, watching the judge decide. A phone locks in that window,
// the WebSocket drops, and the player comes back to the reconnect modal instead of
// the round. Blazor recovers, but the table has already noticed.
//
// The API only exists in a secure context, so plain-HTTP LAN testing gets nothing
// and the caller must not depend on it. Every failure here is non-fatal by design.
let sentinel = null;
let wanted = false;

async function acquire() {
  if (!wanted || sentinel || !('wakeLock' in navigator) || document.visibilityState !== 'visible') {
    return;
  }
  try {
    sentinel = await navigator.wakeLock.request('screen');
    // The browser releases the lock on its own whenever the page is hidden; drop our
    // handle to it too so the visibility listener below knows to ask again.
    sentinel.addEventListener('release', () => { sentinel = null; });
  } catch {
    // Denied, unsupported, or not a secure context. Nothing to recover — the phone
    // just behaves the way it did before this file existed.
    sentinel = null;
  }
}

document.addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'visible') {
    acquire();
  }
});

export function enable() {
  wanted = true;
  return acquire();
}

export async function disable() {
  wanted = false;
  const held = sentinel;
  sentinel = null;
  try { await held?.release(); } catch { /* already gone */ }
}
