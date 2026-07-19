# Siphon Bot

The Telegram face of Siphon. Send it a link, pick a quality, get your video, music or pictures right in the chat.

It's a fully separate app — it only talks to the Siphon backend over HTTP.

## Run it

1. Get a token from [@BotFather](https://t.me/BotFather).
2. Give it to the bot (pick one):

```bash
# user-secrets (nice for dev)
cd src/Siphon.Bot
dotnet user-secrets set Bot:Token "123456:ABC..."

# or an environment variable
set Bot__Token=123456:ABC...
```

3. Make sure the backend is running (default `http://localhost:5046`), then:

```bash
dotnet run --project src/Siphon.Bot
```

That's it. The SQLite database (`bot.db`) is created automatically on first start.

## Docker

```bash
docker build -t siphon-bot .
docker run -d --network siphon_default -e Bot__Token=123456:ABC... -e Backend__BaseUrl=http://api:8080 -e Backend__ApiKey=... -v siphon-bot-data:/data siphon-bot
```

Bu yerda `api` — `docker/docker-compose.yml`dagi backend xizmatining nomi, `siphon_default` esa o'sha compose yaratadigan tarmoq. Botni odatda alohida `docker run` bilan emas, compose orqali ishga tushirgan qulayroq (backend bilan bir tarmoqda bo'ladi).

## Channel gate (optional)

Want users to join your channel before using the bot? In config:

```json
"Gate": { "Enabled": true, "Channel": "@your_channel" }
```

One important thing: the bot must be an **admin** of that channel, otherwise it can't check who joined.

## The 50 MB thing

Telegram lets bots upload files up to 50 MB — that's their rule, not ours. Bigger variants show up with a ⛔ and can't be sent.

If you ever need more, run your own [telegram-bot-api](https://github.com/tdlib/telegram-bot-api) server (it raises the limit to 2 GB) and point the bot at it:

```json
"Bot": { "ApiBaseUrl": "http://localhost:8081" }
```

## Mini App (settings in a web page)

Inline keyboard buttons can't be colored, so the richer settings live in a small Telegram Mini App. The bot now runs a tiny web server (Kestrel) next to the long-polling loop — same process, same database. It serves the Mini App page and a small `initData`-authenticated API where a user edits their own per-platform defaults and the defaults of every group they own.

It listens on `MiniApp:Port` (default `8090`; `ASPNETCORE_URLS` works too). In `config`:

```json
"MiniApp": { "BaseUrl": "https://yourdomain.com/app", "Port": 8090, "InitDataTtlHours": 24 }
```

Telegram only opens Mini Apps over **HTTPS**, so `MiniApp:BaseUrl` must be a public HTTPS URL. Put a reverse proxy in front of the bot's `8090` (or serve it on a path of a domain you already have) and point `BaseUrl` there. On startup the bot sets its menu button ("Sozlamalar") to that URL.

Leave `MiniApp:BaseUrl` empty in dev — the web host still starts (so you can open `http://localhost:8090/` locally), but the menu button is skipped since Telegram would reject a non-HTTPS URL.

## Webhooks?

The bot uses long polling — zero setup, works everywhere. If you ever outgrow it, the update pipeline doesn't care where updates come from: swap `PollingService` for a small webhook endpoint and everything else stays the same.
