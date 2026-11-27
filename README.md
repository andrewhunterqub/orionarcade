# Orion Arcade

Free, fun games. A central library of Unity WebGL games, hosted at **https://arcade.oriondesign.xyz**.

The site is plain static HTML/CSS/JS with no framework and no build step. It's deployed to GitHub Pages on every push to `main`.

```
site/                    <- everything that gets published
  index.html             game library (search + tag filters)
  play.html              player page: play.html?game=<slug>
  assets/                styles and scripts
  games/
    games.json           the game manifest
    <slug>/              one folder per game (Unity WebGL build output)
      Build/
      StreamingAssets/   (optional)
      thumbnail.png      (optional, 16:10)
  CNAME                  custom domain for GitHub Pages
scripts/                 helper scripts (Node 20+, no dependencies)
```

## Games

| Game | Type | Source |
|---|---|---|
| Let's Own A Bar! | Unity 6 WebGL | [`projects/LOAB`](projects/LOAB) (see its README) |
| Pythle | Plain web app | Copied from the Pythle project; rebuild its data there and copy the 5 runtime files |

## Adding a game

Plain HTML/JS games work too: put the game in `site/games/<slug>/` with an `index.html` (and no `Build/` folder), then run `npm run add-game -- <slug>`. It's played in a sandboxed iframe.


1. **Set up Unity's WebGL build** (Player Settings > WebGL > Publishing Settings):
   - **Compression Format:** `Disabled` is simplest. If you want `Brotli` or `Gzip`, also tick **Decompression Fallback**. GitHub Pages can't send the `Content-Encoding` headers Unity needs for `.br`/`.gz` files.
   - **WebGL Template:** any template works. Only the `Build/` folder is used, because the arcade has its own player page.
2. **Build** into `site/games/<slug>/`, where the slug is lowercase with dashes, e.g. `site/games/space-dodge/`. Unity's `index.html` and `TemplateData/` can be deleted.
3. **Register it:**
   ```sh
   npm run add-game -- space-dodge --title "Space Dodge" \
     --description "Weave through an asteroid field." \
     --tags arcade,space --controls "Arrow keys to move, Space to boost"
   ```
   This detects the build files and adds an entry to `games.json`. Run it again after every rebuild, because file names can change. Fields you've already set are kept.
4. Optionally add `site/games/<slug>/thumbnail.png` (16:10, e.g. 640×400) and run `add-game` again. Without a thumbnail, the card shows a coloured placeholder.
5. **Test locally:** `npm run serve` and open http://localhost:8080. Unity builds don't run from `file://`.
6. **Check and push:** `npm run check`, then commit and push to `main`.

### Manifest fields

| Field | Required | Notes |
|---|---|---|
| `slug` | yes | Folder name under `site/games/` and the `?game=` URL parameter |
| `title` | yes | |
| `build` | yes | `loader`, `framework`, `data`, `code` paths (filled in by `add-game`) |
| `description` | | One or two sentences for the card and player page |
| `tags` | | e.g. `["arcade", "puzzle"]`; used for the filter chips |
| `controls` | | Shown under the game |
| `thumbnail` | | Path relative to the game folder |
| `aspect` | | Player aspect ratio, default `16:9` |
| `mobile` | | Set `false` to warn phone/tablet players |
| `added` | | `YYYY-MM-DD`, newest games are listed first |
| `version`, `company` | | Passed to Unity |
| `hidden` | | `true` keeps a game out of the library but still playable by direct link |
| `type`, `entry` | | `"html"` and an entry page for plain web games (no Unity), set automatically by `add-game` |

### Size limits

GitHub rejects files over 100 MB, and a Pages site should stay under 1 GB in total. `npm run check` flags any file over the limit. If builds get large, use compression with Decompression Fallback, strip unused assets, or move to a host that supports large files and compression headers (Cloudflare Pages, Netlify, S3 + CloudFront). The site works unchanged on any static host.

## Deployment

`.github/workflows/deploy.yml` validates the manifest on every PR and push, then publishes `site/` to GitHub Pages from `main`.

One-off setup:

1. **GitHub:** Settings > Pages > Source: **GitHub Actions**. After the first deploy, set the custom domain to `arcade.oriondesign.xyz` (the `site/CNAME` file also does this) and tick **Enforce HTTPS** once the certificate is issued.
2. **DNS** for `oriondesign.xyz`: add a `CNAME` record `arcade` → `andrewhunterqub.github.io`.
3. Optional: verify the domain under your GitHub account's Pages settings to prevent takeover.

## Scripts

| Command | What it does |
|---|---|
| `npm run serve` | Local server at :8080 (sets compression headers for `.br`/`.gz`) |
| `npm run add-game -- <slug> [options]` | Detect a build and add or update its manifest entry |
| `npm run check` | Validate `games.json` and that all referenced files exist |
