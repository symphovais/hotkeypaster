import { Env } from './types';
import { getGoogleAuthUrl, handleOAuthCallback } from './auth';
import {
  handleCors,
  authenticate,
  handleGetUsage,
  handleWhisperProxy,
  handleCleanProxy,
  handleGetProfile
} from './api';

// HTML page templates
const homePage = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>TalkKeys - Voice to Text, Instantly</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; }
    .container { background: white; border-radius: 24px; padding: 48px; max-width: 600px; margin: 24px; box-shadow: 0 25px 50px -12px rgba(0,0,0,0.25); }
    .logo { width: 80px; height: 80px; background: #7C3AED; border-radius: 20px; display: flex; align-items: center; justify-content: center; margin: 0 auto 24px; }
    .logo svg { width: 48px; height: 48px; fill: white; }
    h1 { font-size: 32px; color: #111827; text-align: center; margin-bottom: 8px; }
    .tagline { color: #6B7280; text-align: center; font-size: 18px; margin-bottom: 32px; }
    .features { list-style: none; margin-bottom: 32px; }
    .features li { padding: 12px 0; color: #374151; display: flex; align-items: center; gap: 12px; }
    .features li::before { content: "✓"; color: #7C3AED; font-weight: bold; }
    .download-btn { display: block; background: #7C3AED; color: white; text-decoration: none; padding: 16px 32px; border-radius: 12px; text-align: center; font-weight: 600; font-size: 16px; transition: background 0.2s; }
    .download-btn:hover { background: #6D28D9; }
    .footer { margin-top: 32px; text-align: center; font-size: 14px; color: #9CA3AF; }
    .footer a { color: #7C3AED; text-decoration: none; }
    .footer a:hover { text-decoration: underline; }
  </style>
</head>
<body>
  <div class="container">
    <div class="logo">
      <svg viewBox="0 0 24 24"><path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1 1.93c-3.94-.49-7-3.85-7-7.93h2c0 3.31 2.69 6 6 6s6-2.69 6-6h2c0 4.08-3.06 7.44-7 7.93V19h4v2H8v-2h4v-3.07z"/></svg>
    </div>
    <h1>TalkKeys</h1>
    <p class="tagline">Voice to text, instantly</p>
    <ul class="features">
      <li>Press a hotkey, speak, and text appears where your cursor is</li>
      <li>AI-powered transcription with automatic text cleaning</li>
      <li>10 minutes of free transcription every day</li>
      <li>Works with any application - emails, documents, chat</li>
      <li>Lightweight and runs in the system tray</li>
    </ul>
    <a href="https://github.com/symphovais/hotkeypaster/releases/latest" class="download-btn">Download for Windows</a>
    <div class="footer">
      <p><a href="/privacy">Privacy Policy</a> · <a href="/tos">Terms of Service</a></p>
      <p style="margin-top: 8px;">© 2024 TalkKeys. All rights reserved.</p>
    </div>
  </div>
</body>
</html>`;

const privacyPage = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Privacy Policy - TalkKeys</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #F9FAFB; color: #374151; line-height: 1.7; }
    .header { background: #7C3AED; color: white; padding: 48px 24px; text-align: center; }
    .header h1 { font-size: 32px; margin-bottom: 8px; }
    .header p { opacity: 0.9; }
    .content { max-width: 800px; margin: 0 auto; padding: 48px 24px; }
    h2 { color: #111827; margin: 32px 0 16px; font-size: 24px; }
    p { margin-bottom: 16px; }
    ul { margin: 16px 0 16px 24px; }
    li { margin-bottom: 8px; }
    .back { display: inline-block; margin-top: 32px; color: #7C3AED; text-decoration: none; }
    .back:hover { text-decoration: underline; }
    .updated { color: #9CA3AF; font-size: 14px; margin-top: 48px; }
  </style>
</head>
<body>
  <div class="header">
    <h1>Privacy Policy</h1>
    <p>TalkKeys</p>
  </div>
  <div class="content">
    <h2>Overview</h2>
    <p>TalkKeys is committed to protecting your privacy. This policy explains how we handle your data when you use our voice-to-text application.</p>

    <h2>Data We Collect</h2>
    <ul>
      <li><strong>Account Information:</strong> When you sign in with Google, we receive your email address and name to identify your account.</li>
      <li><strong>Usage Data:</strong> We track the duration of audio processed to enforce daily limits (10 minutes/day for free accounts).</li>
    </ul>

    <h2>Data We Do NOT Collect or Store</h2>
    <ul>
      <li><strong>Audio Recordings:</strong> Your voice recordings are streamed directly to our transcription provider (Groq) and are not stored by TalkKeys.</li>
      <li><strong>Transcribed Text:</strong> The text generated from your speech is sent directly to your device and is not stored on our servers.</li>
      <li><strong>Clipboard Data:</strong> We never access or store your clipboard contents.</li>
    </ul>

    <h2>Third-Party Services</h2>
    <p>We use the following third-party services:</p>
    <ul>
      <li><strong>Google OAuth:</strong> For secure authentication</li>
      <li><strong>Groq API:</strong> For speech-to-text transcription and text processing</li>
      <li><strong>Cloudflare:</strong> For hosting our backend services</li>
    </ul>

    <h2>Data Security</h2>
    <p>All data transmitted between your device and our servers is encrypted using TLS. We do not sell or share your personal information with third parties.</p>

    <h2>Your Rights</h2>
    <p>You can delete your account at any time by contacting us. Upon deletion, all associated usage data will be permanently removed.</p>

    <h2>Contact</h2>
    <p>For privacy concerns, contact us at: privacy@symphoneytek.dk</p>

    <p class="updated">Last updated: December 2024</p>
    <a href="/" class="back">← Back to Home</a>
  </div>
</body>
</html>`;

const tosPage = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Terms of Service - TalkKeys</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #F9FAFB; color: #374151; line-height: 1.7; }
    .header { background: #7C3AED; color: white; padding: 48px 24px; text-align: center; }
    .header h1 { font-size: 32px; margin-bottom: 8px; }
    .header p { opacity: 0.9; }
    .content { max-width: 800px; margin: 0 auto; padding: 48px 24px; }
    h2 { color: #111827; margin: 32px 0 16px; font-size: 24px; }
    p { margin-bottom: 16px; }
    ul { margin: 16px 0 16px 24px; }
    li { margin-bottom: 8px; }
    .back { display: inline-block; margin-top: 32px; color: #7C3AED; text-decoration: none; }
    .back:hover { text-decoration: underline; }
    .updated { color: #9CA3AF; font-size: 14px; margin-top: 48px; }
  </style>
</head>
<body>
  <div class="header">
    <h1>Terms of Service</h1>
    <p>TalkKeys</p>
  </div>
  <div class="content">
    <h2>Acceptance of Terms</h2>
    <p>By using TalkKeys, you agree to these terms of service. If you do not agree, please do not use the application.</p>

    <h2>Service Description</h2>
    <p>TalkKeys is a voice-to-text application that allows you to transcribe speech into text using a keyboard shortcut. The service includes:</p>
    <ul>
      <li>Voice transcription via AI</li>
      <li>Automatic text cleaning and formatting</li>
      <li>Automatic pasting of transcribed text</li>
    </ul>

    <h2>Free Tier Limitations</h2>
    <p>Free accounts include 10 minutes of transcription per day. This limit resets at midnight UTC. We reserve the right to modify these limits at any time.</p>

    <h2>Acceptable Use</h2>
    <p>You agree not to:</p>
    <ul>
      <li>Use the service for any illegal purposes</li>
      <li>Attempt to circumvent usage limits</li>
      <li>Reverse engineer or modify the application</li>
      <li>Use automated systems to abuse the service</li>
      <li>Share your account credentials with others</li>
    </ul>

    <h2>Disclaimer of Warranties</h2>
    <p>TalkKeys is provided "as is" without warranties of any kind. We do not guarantee that the service will be uninterrupted, error-free, or that transcriptions will be 100% accurate.</p>

    <h2>Limitation of Liability</h2>
    <p>TalkKeys shall not be liable for any indirect, incidental, or consequential damages arising from your use of the service.</p>

    <h2>Changes to Terms</h2>
    <p>We may update these terms at any time. Continued use of the service after changes constitutes acceptance of the new terms.</p>

    <h2>Termination</h2>
    <p>We reserve the right to terminate accounts that violate these terms or abuse the service.</p>

    <h2>Contact</h2>
    <p>For questions about these terms, contact us at: support@symphoneytek.dk</p>

    <p class="updated">Last updated: December 2024</p>
    <a href="/" class="back">← Back to Home</a>
  </div>
</body>
</html>`;

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname.toLowerCase();
    const method = request.method;

    // Handle CORS preflight
    if (method === 'OPTIONS') {
      return handleCors();
    }

    try {
      // Public routes (no auth required)

      // Home page
      if (path === '/') {
        return new Response(homePage, {
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      }

      // Health check (API)
      if (path === '/health') {
        return new Response(JSON.stringify({
          status: 'ok',
          service: 'TalkKeys API',
          version: '1.0.5',
          build: '2024-12-10T11:15:00Z'
        }), {
          headers: { 'Content-Type': 'application/json' }
        });
      }

      // Privacy Policy
      if (path === '/privacy') {
        return new Response(privacyPage, {
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      }

      // Terms of Service
      if (path === '/tos') {
        return new Response(tosPage, {
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      }

      // Start Google OAuth flow
      if (path === '/auth/google' && method === 'GET') {
        // Get the desktop app's localhost callback URL from query params
        const desktopCallbackUrl = url.searchParams.get('callback_url') || undefined;
        const authUrl = getGoogleAuthUrl(request, env, desktopCallbackUrl);
        return Response.redirect(authUrl, 302);
      }

      // OAuth callback (Google redirects here)
      if (path === '/auth/callback' && method === 'GET') {
        return handleOAuthCallback(request, env);
      }

      // Protected routes (auth required)

      // Authenticate the request
      const user = await authenticate(request, env);
      if (!user) {
        return new Response(JSON.stringify({
          success: false,
          error: 'Unauthorized. Please sign in.'
        }), {
          status: 401,
          headers: {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*'
          }
        });
      }

      // User profile
      if (path === '/api/profile' && method === 'GET') {
        return handleGetProfile(request, env, user);
      }

      // Usage stats
      if (path === '/api/usage' && method === 'GET') {
        return handleGetUsage(request, env, user);
      }

      // Whisper transcription proxy
      if (path === '/api/whisper' && method === 'POST') {
        return handleWhisperProxy(request, env, user);
      }

      // Text cleaning proxy
      if (path === '/api/clean' && method === 'POST') {
        return handleCleanProxy(request, env, user);
      }

      // 404 for unknown routes
      return new Response(JSON.stringify({
        success: false,
        error: 'Not found'
      }), {
        status: 404,
        headers: { 'Content-Type': 'application/json' }
      });

    } catch (err) {
      console.error('Unhandled error:', err);
      const message = err instanceof Error ? err.message : 'Internal server error';
      return new Response(JSON.stringify({
        success: false,
        error: message
      }), {
        status: 500,
        headers: { 'Content-Type': 'application/json' }
      });
    }
  }
};
