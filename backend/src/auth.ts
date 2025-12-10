import { Env, GoogleTokenResponse, GoogleUserInfo, AuthTokens, JWTPayload } from './types';
import { signJWT, generateRefreshToken } from './jwt';
import { getUserByGoogleId, createUser, updateLastLogin } from './db';

const GOOGLE_AUTH_URL = 'https://accounts.google.com/o/oauth2/v2/auth';
const GOOGLE_TOKEN_URL = 'https://oauth2.googleapis.com/token';
const GOOGLE_USERINFO_URL = 'https://www.googleapis.com/oauth2/v2/userinfo';

// Redirect URI - the callback endpoint on our worker
function getRedirectUri(request: Request): string {
  const url = new URL(request.url);
  return `${url.origin}/auth/callback`;
}

// State contains the desktop app's localhost callback URL
interface OAuthState {
  nonce: string;
  callbackUrl?: string;  // Desktop app's localhost callback URL
}

// Generate Google OAuth URL
export function getGoogleAuthUrl(request: Request, env: Env, desktopCallbackUrl?: string): string {
  // Encode the desktop callback URL in the state parameter
  const stateObj: OAuthState = {
    nonce: generateNonce(),
    callbackUrl: desktopCallbackUrl
  };
  const state = btoa(JSON.stringify(stateObj));

  const params = new URLSearchParams({
    client_id: env.GOOGLE_CLIENT_ID,
    redirect_uri: getRedirectUri(request),
    response_type: 'code',
    scope: 'openid email profile',
    state: state,
    access_type: 'offline',
    prompt: 'consent'
  });

  return `${GOOGLE_AUTH_URL}?${params}`;
}

// Generate a random nonce for state
function generateNonce(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
}

// Exchange authorization code for tokens
async function exchangeCodeForTokens(
  code: string,
  request: Request,
  env: Env
): Promise<GoogleTokenResponse> {
  const response = await fetch(GOOGLE_TOKEN_URL, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded'
    },
    body: new URLSearchParams({
      code,
      client_id: env.GOOGLE_CLIENT_ID,
      client_secret: env.GOOGLE_CLIENT_SECRET,
      redirect_uri: getRedirectUri(request),
      grant_type: 'authorization_code'
    })
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(`Token exchange failed: ${error}`);
  }

  return response.json();
}

// Get user info from Google
async function getGoogleUserInfo(accessToken: string): Promise<GoogleUserInfo> {
  const response = await fetch(GOOGLE_USERINFO_URL, {
    headers: {
      Authorization: `Bearer ${accessToken}`
    }
  });

  if (!response.ok) {
    throw new Error('Failed to get user info');
  }

  return response.json();
}

// Create JWT tokens for our app
async function createAuthTokens(
  userId: string,
  email: string,
  name: string | null,
  env: Env
): Promise<AuthTokens> {
  const now = Math.floor(Date.now() / 1000);

  const payload: JWTPayload = {
    sub: userId,
    email,
    name,
    iat: now,
    exp: now + 30 * 24 * 3600 // 30 days
  };

  const accessToken = await signJWT(payload, env.JWT_SECRET);
  const refreshToken = generateRefreshToken();

  return {
    access_token: accessToken,
    refresh_token: refreshToken,
    expires_in: 30 * 24 * 3600
  };
}

// Parse state parameter to get desktop callback URL
function parseState(stateParam: string | null): OAuthState | null {
  if (!stateParam) return null;
  try {
    return JSON.parse(atob(stateParam));
  } catch {
    return null;
  }
}

// Handle OAuth callback
export async function handleOAuthCallback(
  request: Request,
  env: Env
): Promise<Response> {
  const url = new URL(request.url);
  const code = url.searchParams.get('code');
  const error = url.searchParams.get('error');
  const stateParam = url.searchParams.get('state');

  // Parse state to get desktop callback URL
  const state = parseState(stateParam);
  const desktopCallbackUrl = state?.callbackUrl;

  if (error) {
    return createCallbackResponse(false, error, undefined, desktopCallbackUrl);
  }

  if (!code) {
    return createCallbackResponse(false, 'No authorization code', undefined, desktopCallbackUrl);
  }

  try {
    // Exchange code for Google tokens
    const googleTokens = await exchangeCodeForTokens(code, request, env);

    // Get user info from Google
    const userInfo = await getGoogleUserInfo(googleTokens.access_token);

    // Find or create user in our database
    let user = await getUserByGoogleId(env, userInfo.id);

    if (!user) {
      user = await createUser(env, userInfo.id, userInfo.email, userInfo.name);
    } else {
      await updateLastLogin(env, user.id);
    }

    // Create our JWT tokens
    const tokens = await createAuthTokens(user.id, user.email, user.name, env);

    // Return tokens to the desktop app
    return createCallbackResponse(true, null, tokens, desktopCallbackUrl);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Unknown error';
    return createCallbackResponse(false, message, undefined, desktopCallbackUrl);
  }
}

// Create HTML response that sends tokens back to desktop app
function createCallbackResponse(
  success: boolean,
  error: string | null,
  tokens?: AuthTokens,
  desktopCallbackUrl?: string
): Response {
  // If we have a desktop callback URL, redirect there with tokens
  if (desktopCallbackUrl) {
    try {
      const redirectUrl = new URL(desktopCallbackUrl);
      if (success && tokens) {
        redirectUrl.searchParams.set('access_token', tokens.access_token);
        redirectUrl.searchParams.set('refresh_token', tokens.refresh_token);
        redirectUrl.searchParams.set('expires_in', tokens.expires_in.toString());
      } else {
        redirectUrl.searchParams.set('error', error || 'Unknown error');
      }

      // Redirect to the desktop app's localhost listener
      return Response.redirect(redirectUrl.toString(), 302);
    } catch (e) {
      // If URL parsing fails, fall through to HTML response
    }
  }

  // Fallback: show HTML page (for browser testing or if no callback URL)
  const html = `<!DOCTYPE html>
<html>
<head>
  <title>TalkKeys - ${success ? 'Success' : 'Error'}</title>
  <style>
    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100vh;
      margin: 0;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
    }
    .container {
      text-align: center;
      padding: 40px;
      background: rgba(255,255,255,0.1);
      border-radius: 16px;
      backdrop-filter: blur(10px);
    }
    h1 { margin-bottom: 16px; }
    p { opacity: 0.9; }
    .success { color: #10b981; }
    .error { color: #ef4444; }
    code {
      display: block;
      margin-top: 20px;
      padding: 12px;
      background: rgba(0,0,0,0.2);
      border-radius: 8px;
      font-size: 12px;
      word-break: break-all;
    }
  </style>
</head>
<body>
  <div class="container">
    ${success ? `
      <h1 class="success">Authentication Successful!</h1>
      <p>You can close this window and return to TalkKeys.</p>
      <p style="font-size: 12px; opacity: 0.7; margin-top: 20px;">
        If the app doesn't update automatically, copy this code:
      </p>
      <code id="token">${tokens?.access_token || ''}</code>
    ` : `
      <h1 class="error">Authentication Failed</h1>
      <p>${error || 'Unknown error occurred'}</p>
      <p>Please close this window and try again.</p>
    `}
  </div>
</body>
</html>`;

  return new Response(html, {
    headers: { 'Content-Type': 'text/html' }
  });
}

