self.addEventListener('install', event =>
    event.waitUntil(
        caches
            .open('files-dynamic')
            .then((cache) => fetch('/Files').then(response =>
                response.json().then(files => cache.addAll(files))
            ))
    )
);

self.addEventListener('fetch', event => {
    if (event.request.url.includes("/Files/")) {
        event.respondWith(
            caches.open('files-dynamic').then(cache =>
                cache.match(event.request).then(response =>
                    response || fetch(event.request).then(response => {
                        cache.put(event.request, response.clone());
                        return response;
                    })
                )
            )
        );
    } else {
        event.respondWith(fetch(event.request));
    }
});