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
  privacy.html           privacy policy (needed for Facebook sign-in)
  CNAME                  custom domain for GitHub Pages
scripts/                 helper scripts (Node 20+, no dependencies)
unity/OrionArcade/       optional Unity plugin for account saves
firestore.rules          security rules for account saves
```

## Games

| Game | Type | Source |
|---|---|---|
| Let's Open A Bar! | Unity 6 WebGL | [`projects/LOAB`](projects/LOAB) (see its README) |
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

### Size rule (applies to every game and project in this repo)

**GitHub rejects any push containing a file over 100 MB**, and a Pages site should stay under 1 GB in total. One oversized file blocks the whole push, so this is a design constraint from day one, not a cleanup step:

- **Budget:** keep every committed file under **50 MB** (`npm run check` warns above that and fails above 100 MB). Aim for a whole game build in the low tens of MB.
- **Favour small files:** procedural or primitive-built content over imported art, compressed textures and audio (and at sensible resolutions), no raw source media (PSD, WAV, 4K video) in the repo.
- **Split, don't bloat:** break large data into multiple files or modules, and load optional content on demand (e.g. `StreamingAssets/`, Addressables) rather than baking it into one big `.data`.
- **Never commit generated or environment folders:** Unity `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, Python `.venv`/`.conda`/`__pycache__`, sim output folders. The root `.gitignore` blocks these for projects at any depth; keep it that way.
- **Source lives in `projects/<name>/`, builds in `site/games/<slug>/`.** Everything under `site/` is published publicly, so no source projects, keys or certificates there.
- **Run `npm run check` before every commit.** It scans every file git would commit, not just the builds in `games.json`.

If a build still gets large, use compression with Decompression Fallback, strip unused assets/engine modules, or move to a host that supports large files and compression headers (Cloudflare Pages, Netlify, S3 + CloudFront). The site works unchanged on any static host.

## Saving progress

Each game gets its own save slot, whether or not the player is signed in. Unity WebGL keeps `PlayerPrefs` and `Application.persistentDataPath` in one browser database that every game on this site would otherwise share. The player page swaps each game's data in before it starts and copies changes back out while it runs (every 10 seconds and when the tab is hidden).

For this to work, games should call `PlayerPrefs.Save()` after changing PlayerPrefs. Games that write their own files to `persistentDataPath` should call `OrionCloudSave.SyncFiles()` afterwards (see below).

### Accounts and cloud saves (optional)

Players can sign in with Google, Facebook, Microsoft, GitHub or an emailed link. Signed-in players' progress is saved to their account, so they can pick up on any device. Signing in is never required, and with it switched off the site works exactly as it does without accounts.

- When a game starts, the newer of the browser's progress and the account's progress is used.
- If a player signs in mid-game and their account has newer progress, they're asked which to keep.
- A game's save is limited to about 900 KB once compressed (a Firestore limit). Larger saves stay in the browser and the player is told.
- Players can delete their account and saved progress from the account menu. `privacy.html` explains what's stored.

Sign-in uses [Firebase](https://firebase.google.com) (Authentication + Cloud Firestore). The free Spark plan is plenty for an arcade, but it limits how many sign-in emails can be sent each day.

#### Setting up Firebase

1. **Create a project** at https://console.firebase.google.com (Google Analytics isn't needed).
2. **Register the site:** Project settings > General > Your apps > Web (`</>`). Copy the `firebaseConfig` values into `site/assets/firebase-config.js`. These values are public by design; `firestore.rules` is what protects saved data.
3. **Database:** Build > Firestore Database > Create database, in production mode, in a region near your players (e.g. `europe-west2`, London). On the **Rules** tab, paste in `firestore.rules` and publish.
4. **Allowed domain:** Authentication > Settings > Authorized domains > add `arcade.oriondesign.xyz`. (`localhost` is already allowed for `npm run serve`.)
5. **Turn on sign-in options** under Authentication > Sign-in method. Each social option needs an app made on that company's developer site. All of them ask for the same **redirect URI**, which Firebase shows when you enable the provider: `https://<project-id>.firebaseapp.com/__/auth/handler`.
   - **Google:** enable it and pick a support email.
   - **Email link:** enable Email/Password, then tick **Email link (passwordless sign-in)**. You can set the sender name under Authentication > Templates.
   - **Facebook:** at https://developers.facebook.com create an app with the "Authenticate and request data from users with Facebook Login" use case. Copy the App ID and App secret into Firebase. In Facebook Login > Settings, add the redirect URI. In App settings > Basic, set the Privacy Policy URL to `https://arcade.oriondesign.xyz/privacy.html` and User data deletion to `https://arcade.oriondesign.xyz/privacy.html#delete`. Then switch the app to **Live**. Meta may ask you to verify a business first.
   - **Microsoft:** at https://portal.azure.com go to Microsoft Entra ID > App registrations > New registration. Choose "Accounts in any organizational directory and personal Microsoft accounts" and add the redirect URI as a **Web** platform. Under Certificates & secrets, create a client secret. Copy the Application (client) ID and the secret's **Value** into Firebase. Client secrets expire (24 months at most), so note when to renew it.
   - **GitHub:** at https://github.com/settings/developers create an OAuth App with homepage `https://arcade.oriondesign.xyz` and the redirect URI as its callback URL. Copy the client ID and a new client secret into Firebase.
6. Keep Authentication > Settings > **User account linking** on "Link accounts that use the same email" (the default). A player who signs in with Google and later with Microsoft under the same email is then the same player.
7. Commit `firebase-config.js` and push to `main`. The **Sign in** button appears once a config is present.

To show fewer options, edit `signInMethods` in `firebase-config.js`. To add **Sign in with Apple** later, add `"apple"` to it. Apple sign-in needs an Apple Developer Program membership ($99/year), a Services ID and a key, all set up in the Apple provider settings in Firebase.

### Unity plugin (optional)

`PlayerPrefs` and files are synced without any code changes. For more control, copy `unity/OrionArcade/` into your project's `Assets/` folder and use `OrionArcade.OrionCloudSave`:

```csharp
using OrionArcade;

OrionCloudSave.SetInt("level", 3);
OrionCloudSave.SetJson("inventory", inventory);   // any [Serializable] class
var level = OrionCloudSave.GetInt("level", 1);
if (OrionCloudSave.IsSignedIn) Debug.Log($"Hi {OrionCloudSave.PlayerName}");
OrionCloudSave.Save();        // upload now, e.g. at a checkpoint (otherwise within ~15 s)
OrionCloudSave.SyncFiles();   // after writing your own files to persistentDataPath
```

Outside the arcade (in the Editor, on other platforms or on other websites) `OrionCloudSave` uses `PlayerPrefs`, so the same code works everywhere. On the player page, the same functions are available to JavaScript as `window.OrionArcade` (`load`, `save`, `remove`, `keys`, `flush`, `isSignedIn`, `playerName`).

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
| `npm run check` | Validate `games.json`, that all referenced files exist, and that nothing git would commit is over the size limits |
