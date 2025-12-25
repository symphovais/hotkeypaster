import { Env } from './types';
import { getGoogleAuthUrl, handleOAuthCallback } from './auth';
import {
  handleCors,
  authenticate,
  handleGetUsage,
  handleWhisperProxy,
  handleCleanProxy,
  handleExplainProxy,
  handleClassifyProxy,
  handleRewriteProxy,
  handleExtractRemindersProxy,
  handleAnalyzeWordsProxy,
  handleGetProfile
} from './api';

// Import pages
import { homePage } from './pages/home';
import { privacyPage } from './pages/privacy';
import { tosPage } from './pages/tos';
import { apiDocsPage } from './pages/api-docs';
import { generateReleasesPage } from './pages/releases';
import { aboutContent } from './content/about';

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

      // Release Notes (generated from aboutContent)
      if (path === '/releases') {
        return new Response(generateReleasesPage(), {
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      }

      // API Documentation
      if (path === '/api-docs') {
        return new Response(apiDocsPage, {
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      }

      // About Content API (for desktop app)
      if (path === '/about-content' && method === 'GET') {
        return new Response(JSON.stringify(aboutContent), {
          headers: {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
            'Cache-Control': 'public, max-age=3600'
          }
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

      // Plain English explainer proxy
      if (path === '/api/explain' && method === 'POST') {
        return handleExplainProxy(request, env, user);
      }

      // Classification proxy
      if (path === '/api/classify' && method === 'POST') {
        return handleClassifyProxy(request, env, user);
      }

      // Rewrite proxy
      if (path === '/api/rewrite' && method === 'POST') {
        return handleRewriteProxy(request, env, user);
      }

      // Extract reminders/calendar events proxy
      if (path === '/api/extract-reminders' && method === 'POST') {
        return handleExtractRemindersProxy(request, env, user);
      }

      // Words analysis proxy
      if (path === '/api/analyze-words' && method === 'POST') {
        return handleAnalyzeWordsProxy(request, env, user);
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
