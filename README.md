# Siphon

Istalgan havoladan media'ni **asl sifatida** yuklab beruvchi tizim — YouTube, Instagram, TikTok va yt-dlp qo'llab-quvvatlaydigan barcha saytlar. Havolada kontent bo'lsa, sug'urib oladi.

Asosiy tamoyil: **sifat sun'iy oshirilmaydi ham, pasaytirilmaydi ham.** Video stream-copy bilan mp4'ga qadoqlanadi (remux), audio esa manba bitrate'idan kelib chiqib mp3'ga o'giriladi — hech qachon qat'iy 320k emas.

## Tarkib

| Papka | Nima | Stack |
|-------|------|-------|
| `src/backend/` | HTTP API (Siphon.Api + Siphon.Media modul + Siphon.Tests) | .NET 10 |
| `src/web/` | web ilova (build'da backend `wwwroot`'iga tushadi) | Angular 22 (TS) |
| `src/extension/` | Chrome MV3 kengaytmasi (unpacked yuklanadi) | TypeScript + esbuild |
| `src/shared/` | umumiy TS model, format, i18n va dizayn tokenlari — web va extension bo'lishadi | TypeScript |
| `src/bot/` | Telegram bot — to'liq alohida ilova, backendga faqat HTTP orqali | .NET 10 |
| `tools/` | `yt-dlp.exe`, `gallery-dl.exe` (git'ga kirmaydi) | |
| `docker/` | backend Dockerfile, docker-compose | |

VS'da `Siphon.slnx` — barcha .NET loyihalar (backend + bot). Web va extension npm loyihalari; ularni VS papka-ko'rinishida yoki VS Code'da tahrirlash qulay.

## Dev'da ishga tushirish

Talab: .NET 10 SDK, Node 22+, ffmpeg. Binary'lar bir marta:

```bash
curl -L --create-dirs https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe -o tools/yt-dlp.exe
curl -L --create-dirs https://github.com/mikf/gallery-dl/releases/latest/download/gallery-dl.exe -o tools/gallery-dl.exe
```

**Backend** (`appsettings.Development.json` dev kalitlari bilan tayyor):

```bash
dotnet run --project src/backend/Siphon.Api      # http://localhost:5046
dotnet test Siphon.slnx
```

**Web (Angular)** — dev serverda backendga proxy qiladi:

```bash
cd src/web && npm install && npm start           # http://localhost:4200
```

Yoki productionga: `npm run build` → `src/web/dist/siphon-web` → backend build'i uni `wwwroot`'ga ko'chiradi, keyin backend o'zi statik beradi.

**Extension**:

```bash
cd src/extension && npm install && npm run build  # dist/ yaratadi
```

`chrome://extensions` → Developer mode → **Load unpacked** → `src/extension/dist`. (Chrome Web Store YouTube yuklovchilarni qabul qilmaydi, shuning uchun faqat unpacked.)

**Bot**: `src/bot/README.md`ga qarang (token kerak).

## Productionga (VPS, Docker)

```bash
cp docker/.env.example docker/.env   # qiymatlarni to'ldiring
docker compose -f docker/docker-compose.yml up -d --build
```

Backend image Angular'ni ham o'zi quradi (node bosqichi). Datacenter IP'lar YouTube tomonidan tez-tez bloklanadi — kerak bo'lsa extension cookie'sini ulash yoki `Siphon:ProxyUrl` (residential proxy) ishlatiladi.
