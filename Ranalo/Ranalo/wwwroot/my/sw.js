// Ranalo customer app: the offline helper (service worker).
// It makes the app installable and shows a friendly page when there's no
// connection. It never stores account pages -- balances, statements and
// notifications always come fresh from the server, so nothing about a
// customer can be read from the phone's storage.
const CACHE = 'ranalo-app-v1';
const OFFLINE = '/my/offline.html';
const STATIC = [OFFLINE, '/images/brand/ranalo-icon-192.png', '/images/brand/ranalo-icon.png'];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(STATIC)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  // Pages: always the network; the offline page only when there is none.
  if (req.mode === 'navigate') {
    event.respondWith(fetch(req).catch(() => caches.match(OFFLINE)));
    return;
  }

  // The app's own icons: from the cache when we have them.
  if (STATIC.includes(new URL(req.url).pathname)) {
    event.respondWith(caches.match(req).then((hit) => hit || fetch(req)));
  }
});
