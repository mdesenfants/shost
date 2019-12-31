self.addEventListener('install', event =>
    event.waitUntil(
        caches
            .open('files-dynamic')
            .then((cache) => fetch('/Files').then(response =>
                response.json().then(files => cache.addAll(files))
            ))
    )
);