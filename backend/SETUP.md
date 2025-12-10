# TalkKeys Backend Setup Guide

## Prerequisites
- Node.js 18+ installed
- Cloudflare account (free tier works)
- Google Cloud Console account (for OAuth)

## Step 1: Install Dependencies

```bash
cd backend
pnpm install
```

## Step 2: Cloudflare Setup

### 2.1 Login to Cloudflare
```bash
npx wrangler login
```

### 2.2 Create D1 Database
```bash
npx wrangler d1 create talkkeys-db
```

Copy the `database_id` from the output and update `wrangler.toml`:
```toml
[[d1_databases]]
binding = "DB"
database_name = "talkkeys-db"
database_id = "YOUR_DATABASE_ID_HERE"
```

### 2.3 Run Database Migrations
```bash
# Local development
npm run db:migrate:local

# Production
npm run db:migrate
```

## Step 3: Google OAuth Setup

### 3.1 Create Google Cloud Project
1. Go to https://console.cloud.google.com/
2. Create a new project named "TalkKeys"
3. Enable the "Google+ API" (or "Google Identity" API)

### 3.2 Configure OAuth Consent Screen
1. Go to "APIs & Services" > "OAuth consent screen"
2. Choose "External" user type
3. Fill in:
   - App name: TalkKeys
   - User support email: your email
   - Developer contact: your email
4. Add scopes: `email`, `profile`, `openid`
5. Save and continue

### 3.3 Create OAuth Credentials
1. Go to "APIs & Services" > "Credentials"
2. Click "Create Credentials" > "OAuth client ID"
3. Application type: "Web application"
4. Name: "TalkKeys Web Client"
5. Authorized redirect URIs:
   - For development: `http://localhost:8787/auth/callback`
   - For production: `https://talkkeys.symphonytek.dk/auth/callback`
6. Click "Create"
7. Copy the **Client ID** and **Client Secret**

## Step 4: Configure Secrets

```bash
# Google OAuth
npx wrangler secret put GOOGLE_CLIENT_ID
# Paste your client ID

npx wrangler secret put GOOGLE_CLIENT_SECRET
# Paste your client secret

# Groq API Key (your master key)
npx wrangler secret put GROQ_API_KEY
# Paste your Groq API key

# JWT Secret (generate a random 32+ character string)
npx wrangler secret put JWT_SECRET
# Paste a random string like: your-super-secret-jwt-key-here-make-it-long
```

## Step 5: Custom Domain Setup

### 5.1 In Cloudflare Dashboard
1. Add your domain to Cloudflare (if not already)
2. Go to Workers & Pages > your worker
3. Click "Settings" > "Triggers"
4. Add Custom Domain: `talkkeys.symphonytek.dk`

### 5.2 Update wrangler.toml
Uncomment the routes line:
```toml
routes = [{ pattern = "talkkeys.symphonytek.dk", custom_domain = true }]
```

## Step 6: Deploy

### Development (local)
```bash
pnpm dev
```
Worker runs at http://localhost:8787

### Production
```bash
pnpm deploy
```

## Step 7: Test the API

### Health check
```bash
curl https://talkkeys.symphonytek.dk/health
```

### Start OAuth flow
Open in browser: `https://talkkeys.symphonytek.dk/auth/google`

## API Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/health` | GET | No | Health check |
| `/auth/google` | GET | No | Start OAuth flow |
| `/auth/callback` | GET | No | OAuth callback |
| `/api/profile` | GET | Yes | Get user profile |
| `/api/usage` | GET | Yes | Get usage stats |
| `/api/whisper` | POST | Yes | Transcribe audio |
| `/api/clean` | POST | Yes | Clean text |

## Troubleshooting

### "Unauthorized" errors
- Check that JWT_SECRET is set correctly
- Verify the access token is being sent in Authorization header

### OAuth redirect errors
- Verify redirect URI in Google Console matches exactly
- Check GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET are set

### Database errors
- Run migrations: `pnpm db:migrate`
- Check database_id in wrangler.toml

## Cost Estimate

| Service | Free Tier | Notes |
|---------|-----------|-------|
| Workers | 100k req/day | More than enough |
| D1 | 5GB, 5M reads/day | Minimal usage |
| Groq | Pay per use | ~$0.001/request |

**Total: ~$1-5/month** (just Groq API costs)
