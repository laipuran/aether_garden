# Apple Music data is fetched via a scraped developer token

`AppleMusicService` reads playlists and songs from Apple's private web API (`amp-api.music.apple.com`), which demands a `Bearer` token. Instead of registering a MusicKit developer token, we scrape one at runtime: download Apple's web-player JavaScript bundle and extract a JWT from it by regex.

Scraping needs no credentials or Apple account, but is fragile by nature — Apple can move the bundle, change the token format, or block the endpoint at any time. We accept this: on failure the module degrades to empty results (tracks are skipped, conversions return not-found) rather than crashing, and the endpoints are best-effort. If the scraping stops working, the fallback is to generate our own MusicKit token from a registered developer key.
