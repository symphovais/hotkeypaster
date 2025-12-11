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
  <meta name="description" content="Transform your voice into text instantly with TalkKeys. Press a hotkey, speak naturally, and watch your words appear anywhere you type.">
  <link rel="icon" type="image/png" href="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/icon-talkkeys.png">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0F0F1A; color: #E5E7EB; line-height: 1.6; overflow-x: hidden; }

    /* Animated gradient background */
    .hero { min-height: 100vh; position: relative; display: flex; align-items: center; justify-content: center; padding: 80px 24px; }
    .hero::before { content: ''; position: absolute; inset: 0; background: radial-gradient(ellipse at 30% 20%, rgba(124, 58, 237, 0.3) 0%, transparent 50%), radial-gradient(ellipse at 70% 80%, rgba(99, 102, 241, 0.2) 0%, transparent 50%); }
    .hero-content { position: relative; z-index: 1; max-width: 1200px; width: 100%; }

    /* Logo and branding */
    .brand { display: flex; align-items: center; justify-content: center; gap: 16px; margin-bottom: 24px; }
    .logo { width: 72px; height: 72px; border-radius: 18px; box-shadow: 0 20px 40px -12px rgba(124, 58, 237, 0.5); }
    .brand-name { font-size: 42px; font-weight: 700; background: linear-gradient(135deg, #fff 0%, #a5b4fc 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }

    /* Hero text */
    .hero-title { font-size: clamp(32px, 5vw, 56px); font-weight: 800; text-align: center; margin-bottom: 16px; line-height: 1.2; }
    .hero-title span { background: linear-gradient(135deg, #7C3AED 0%, #A78BFA 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .hero-subtitle { font-size: 20px; color: #9CA3AF; text-align: center; max-width: 600px; margin: 0 auto 40px; }

    /* CTA buttons */
    .cta-group { display: flex; gap: 16px; justify-content: center; flex-wrap: wrap; margin-bottom: 16px; }
    .btn { display: inline-flex; align-items: center; gap: 10px; padding: 16px 32px; border-radius: 12px; font-weight: 600; font-size: 16px; text-decoration: none; transition: all 0.3s ease; }
    .btn-primary { background: linear-gradient(135deg, #7C3AED 0%, #6366F1 100%); color: white; box-shadow: 0 10px 30px -10px rgba(124, 58, 237, 0.5); }
    .btn-primary:hover { transform: translateY(-2px); box-shadow: 0 15px 40px -10px rgba(124, 58, 237, 0.6); }
    .btn-store { background: linear-gradient(135deg, #0078D4 0%, #106EBE 100%); color: white; box-shadow: 0 10px 30px -10px rgba(0, 120, 212, 0.5); }
    .btn-store:hover { transform: translateY(-2px); box-shadow: 0 15px 40px -10px rgba(0, 120, 212, 0.6); }
    .btn-secondary { background: rgba(255,255,255,0.1); color: white; border: 1px solid rgba(255,255,255,0.2); }
    .btn-secondary:hover { background: rgba(255,255,255,0.15); }
    .btn svg { width: 20px; height: 20px; fill: currentColor; }

    /* Store badge */
    .store-badge { display: flex; align-items: center; justify-content: center; gap: 8px; color: #6B7280; font-size: 14px; margin-top: 12px; }
    .store-badge svg { width: 18px; height: 18px; }

    /* Developer mode note */
    .dev-note { text-align: center; color: #6B7280; font-size: 13px; margin-top: 12px; }
    .dev-note a { color: #A78BFA; text-decoration: none; }
    .dev-note a:hover { text-decoration: underline; }

    /* Windows Store coming soon */
    .store-coming-soon { display: flex; align-items: center; justify-content: center; gap: 16px; margin-top: 32px; padding: 20px 32px; background: linear-gradient(135deg, rgba(0, 120, 212, 0.15) 0%, rgba(99, 102, 241, 0.15) 100%); border: 1px solid rgba(0, 120, 212, 0.3); border-radius: 16px; }
    .store-coming-soon svg { width: 36px; height: 36px; color: #60A5FA; flex-shrink: 0; }
    .store-coming-soon > div { display: flex; flex-direction: column; gap: 2px; }
    .store-title { font-size: 18px; font-weight: 600; color: #E5E7EB; }
    .store-subtitle { font-size: 14px; color: #9CA3AF; }

    /* Screenshots */
    .screenshots { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 24px; margin-top: 64px; }
    .screenshot { border-radius: 16px; overflow: hidden; box-shadow: 0 25px 50px -12px rgba(0,0,0,0.5); border: 1px solid rgba(255,255,255,0.1); transition: transform 0.3s ease; }
    .screenshot:hover { transform: scale(1.02); }
    .screenshot img { width: 100%; height: auto; display: block; }
    .screenshot-caption { background: rgba(0,0,0,0.6); padding: 12px 16px; font-size: 14px; color: #9CA3AF; }

    /* Features section */
    .features { padding: 100px 24px; background: linear-gradient(180deg, #0F0F1A 0%, #1a1a2e 100%); }
    .features-container { max-width: 1200px; margin: 0 auto; }
    .section-title { font-size: 36px; font-weight: 700; text-align: center; margin-bottom: 16px; }
    .section-subtitle { color: #9CA3AF; text-align: center; margin-bottom: 64px; font-size: 18px; }
    .features-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 32px; }
    .feature-card { background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; padding: 32px; transition: all 0.3s ease; }
    .feature-card:hover { background: rgba(255,255,255,0.06); border-color: rgba(124, 58, 237, 0.3); transform: translateY(-4px); }
    .feature-icon { width: 48px; height: 48px; background: linear-gradient(135deg, rgba(124, 58, 237, 0.2) 0%, rgba(99, 102, 241, 0.2) 100%); border-radius: 12px; display: flex; align-items: center; justify-content: center; margin-bottom: 20px; }
    .feature-icon svg { width: 24px; height: 24px; fill: #A78BFA; }
    .feature-title { font-size: 20px; font-weight: 600; margin-bottom: 12px; }
    .feature-desc { color: #9CA3AF; font-size: 15px; }

    /* How it works */
    .how-it-works { padding: 100px 24px; background: #0F0F1A; }
    .steps { display: flex; flex-wrap: wrap; justify-content: center; gap: 48px; max-width: 1000px; margin: 0 auto; }
    .step { text-align: center; flex: 1; min-width: 200px; }
    .step-number { width: 56px; height: 56px; background: linear-gradient(135deg, #7C3AED 0%, #6366F1 100%); border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 24px; font-weight: 700; margin: 0 auto 20px; }
    .step-title { font-size: 18px; font-weight: 600; margin-bottom: 8px; }
    .step-desc { color: #9CA3AF; font-size: 14px; }

    /* Footer */
    footer { padding: 48px 24px; border-top: 1px solid rgba(255,255,255,0.1); text-align: center; }
    .footer-links { display: flex; gap: 24px; justify-content: center; margin-bottom: 24px; flex-wrap: wrap; }
    .footer-links a { color: #9CA3AF; text-decoration: none; font-size: 14px; transition: color 0.2s; }
    .footer-links a:hover { color: #A78BFA; }
    .copyright { color: #6B7280; font-size: 13px; }

    /* Responsive */
    @media (max-width: 768px) {
      .hero { padding: 60px 20px; }
      .brand-name { font-size: 32px; }
      .screenshots { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <!-- Hero Section -->
  <section class="hero">
    <div class="hero-content">
      <div class="brand">
        <img class="logo" src="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/icon-talkkeys.png" alt="TalkKeys">
        <span class="brand-name">TalkKeys</span>
      </div>

      <h1 class="hero-title">Your Voice, <span>Instantly Typed</span></h1>
      <p class="hero-subtitle">Press a hotkey, speak naturally, and watch your words appear anywhere you type. Powered by AI for accurate, clean text every time.</p>

      <div class="cta-group">
        <a href="https://apps.microsoft.com/detail/9P2D7DZQS61J" class="btn btn-store">
          <svg viewBox="0 0 24 24"><path fill="currentColor" d="M3 12V6.75l6-1.32v6.48L3 12zm17-9v8.75l-10 .15V5.21L20 3zM3 13l6 .09v6.81l-6-1.15V13zm17 .25V22l-10-1.91V13.1l10 .15z"/></svg>
          Get it from Microsoft Store
        </a>
        <a href="https://github.com/symphovais/hotkeypaster" class="btn btn-secondary">
          <svg viewBox="0 0 24 24"><path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/></svg>
          View on GitHub
        </a>
      </div>

      <!-- Screenshots -->
      <div class="screenshots">
        <div class="screenshot">
          <img src="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/docs/images/mainscreenshot.png" alt="TalkKeys in action - voice to text transcription">
          <div class="screenshot-caption">🎙️ Recording widget appears while you speak</div>
        </div>
        <div class="screenshot">
          <img src="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/docs/images/mainscreenshot-settings.png" alt="TalkKeys settings">
          <div class="screenshot-caption">⚙️ Simple, clean settings interface</div>
        </div>
      </div>
    </div>
  </section>

  <!-- Features Section -->
  <section class="features">
    <div class="features-container">
      <h2 class="section-title">Why TalkKeys?</h2>
      <p class="section-subtitle">The fastest way to turn your thoughts into text</p>

      <div class="features-grid">
        <div class="feature-card">
          <div class="feature-icon">
            <svg viewBox="0 0 24 24"><path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>
          </div>
          <h3 class="feature-title">10 Minutes Free Daily</h3>
          <p class="feature-desc">Get 10 minutes of free transcription every day. No credit card required, no strings attached.</p>
        </div>

        <div class="feature-card">
          <div class="feature-icon">
            <svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>
          </div>
          <h3 class="feature-title">Works Everywhere</h3>
          <p class="feature-desc">Use TalkKeys in any application - emails, documents, chat apps, code editors, anywhere you type.</p>
        </div>

        <div class="feature-card">
          <div class="feature-icon">
            <svg viewBox="0 0 24 24"><path d="M19.35 10.04A7.49 7.49 0 0 0 12 4C9.11 4 6.6 5.64 5.35 8.04A5.994 5.994 0 0 0 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM17 13l-5 5-5-5h3V9h4v4h3z"/></svg>
          </div>
          <h3 class="feature-title">AI-Powered Cleanup</h3>
          <p class="feature-desc">Automatic punctuation, capitalization, and formatting. Your text comes out clean and professional.</p>
        </div>

        <div class="feature-card">
          <div class="feature-icon">
            <svg viewBox="0 0 24 24"><path d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z"/></svg>
          </div>
          <h3 class="feature-title">Privacy First</h3>
          <p class="feature-desc">Audio is processed and immediately discarded. We never store your recordings or transcribed text.</p>
        </div>

        <div class="feature-card">
          <div class="feature-icon">
            <svg viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>
          </div>
          <h3 class="feature-title">Push-to-Talk or Toggle</h3>
          <p class="feature-desc">Choose your preferred mode. Hold the hotkey to record, or press once to start and again to stop.</p>
        </div>

        <div class="feature-card">
          <div class="feature-icon">
            <svg viewBox="0 0 24 24"><path d="M20 5H4c-1.1 0-1.99.9-1.99 2L2 17c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm-9 3h2v2h-2V8zm0 3h2v2h-2v-2zM8 8h2v2H8V8zm0 3h2v2H8v-2zm-1 2H5v-2h2v2zm0-3H5V8h2v2zm9 7H8v-2h8v2zm0-4h-2v-2h2v2zm0-3h-2V8h2v2zm3 3h-2v-2h2v2zm0-3h-2V8h2v2z"/></svg>
          </div>
          <h3 class="feature-title">Global Hotkeys</h3>
          <p class="feature-desc">Customizable keyboard shortcut works system-wide. Default: Ctrl+Shift+Space.</p>
        </div>
      </div>
    </div>
  </section>

  <!-- How It Works -->
  <section class="how-it-works">
    <div class="features-container">
      <h2 class="section-title">How It Works</h2>
      <p class="section-subtitle">Three simple steps to faster typing</p>

      <div class="steps">
        <div class="step">
          <div class="step-number">1</div>
          <h3 class="step-title">Press the Hotkey</h3>
          <p class="step-desc">Hit Ctrl+Shift+Space (or your custom shortcut) from any application</p>
        </div>
        <div class="step">
          <div class="step-number">2</div>
          <h3 class="step-title">Speak Naturally</h3>
          <p class="step-desc">Talk at your normal pace. The AI handles punctuation and formatting</p>
        </div>
        <div class="step">
          <div class="step-number">3</div>
          <h3 class="step-title">Text Appears</h3>
          <p class="step-desc">Your transcribed text is automatically pasted where your cursor was</p>
        </div>
      </div>
    </div>
  </section>

  <!-- Footer -->
  <footer>
    <div class="footer-links">
      <a href="/privacy">Privacy Policy</a>
      <a href="/tos">Terms of Service</a>
      <a href="https://github.com/symphovais/hotkeypaster">GitHub</a>
      <a href="https://github.com/symphovais/hotkeypaster/issues">Support</a>
    </div>
    <p class="copyright">© 2024 TalkKeys by symphonytek ApS. All rights reserved.</p>
  </footer>
</body>
</html>`;

const privacyPage = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Privacy Policy - TalkKeys</title>
  <link rel="icon" type="image/png" href="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/icon-talkkeys.png">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0F0F1A; color: #E5E7EB; line-height: 1.7; }
    .header { background: linear-gradient(135deg, rgba(124, 58, 237, 0.3) 0%, rgba(99, 102, 241, 0.2) 100%); padding: 64px 24px; text-align: center; border-bottom: 1px solid rgba(255,255,255,0.1); }
    .header h1 { font-size: 36px; margin-bottom: 8px; background: linear-gradient(135deg, #fff 0%, #a5b4fc 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .header p { color: #9CA3AF; }
    .content { max-width: 800px; margin: 0 auto; padding: 64px 24px; }
    h2 { color: #E5E7EB; margin: 40px 0 16px; font-size: 24px; padding-bottom: 8px; border-bottom: 1px solid rgba(255,255,255,0.1); }
    p { margin-bottom: 16px; color: #9CA3AF; }
    ul { margin: 16px 0 16px 24px; color: #9CA3AF; }
    li { margin-bottom: 12px; }
    li strong { color: #A78BFA; }
    .back { display: inline-flex; align-items: center; gap: 8px; margin-top: 40px; color: #A78BFA; text-decoration: none; font-weight: 500; transition: color 0.2s; }
    .back:hover { color: #C4B5FD; }
    .back svg { width: 20px; height: 20px; fill: currentColor; }
    .updated { color: #6B7280; font-size: 14px; margin-top: 48px; padding-top: 24px; border-top: 1px solid rgba(255,255,255,0.1); }
    .page-footer { margin-top: 64px; padding-top: 32px; border-top: 1px solid rgba(255,255,255,0.1); display: flex; flex-wrap: wrap; gap: 24px; justify-content: center; }
    .page-footer a { color: #9CA3AF; text-decoration: none; font-size: 14px; transition: color 0.2s; }
    .page-footer a:hover { color: #A78BFA; }
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
    <p>For privacy concerns, contact us at: privacy@symphonytek.dk</p>

    <p class="updated">Last updated: December 2024</p>
    <div class="page-footer">
      <a href="/">Home</a>
      <a href="/tos">Terms of Service</a>
      <a href="https://github.com/symphovais/hotkeypaster">GitHub</a>
    </div>
  </div>
</body>
</html>`;

const tosPage = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Terms of Service - TalkKeys</title>
  <link rel="icon" type="image/png" href="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/icon-talkkeys.png">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0F0F1A; color: #E5E7EB; line-height: 1.7; }
    .header { background: linear-gradient(135deg, rgba(124, 58, 237, 0.3) 0%, rgba(99, 102, 241, 0.2) 100%); padding: 64px 24px; text-align: center; border-bottom: 1px solid rgba(255,255,255,0.1); }
    .header h1 { font-size: 36px; margin-bottom: 8px; background: linear-gradient(135deg, #fff 0%, #a5b4fc 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .header p { color: #9CA3AF; }
    .content { max-width: 800px; margin: 0 auto; padding: 64px 24px; }
    h2 { color: #E5E7EB; margin: 40px 0 16px; font-size: 24px; padding-bottom: 8px; border-bottom: 1px solid rgba(255,255,255,0.1); }
    p { margin-bottom: 16px; color: #9CA3AF; }
    ul { margin: 16px 0 16px 24px; color: #9CA3AF; }
    li { margin-bottom: 12px; }
    li strong { color: #A78BFA; }
    .back { display: inline-flex; align-items: center; gap: 8px; margin-top: 40px; color: #A78BFA; text-decoration: none; font-weight: 500; transition: color 0.2s; }
    .back:hover { color: #C4B5FD; }
    .back svg { width: 20px; height: 20px; fill: currentColor; }
    .updated { color: #6B7280; font-size: 14px; margin-top: 48px; padding-top: 24px; border-top: 1px solid rgba(255,255,255,0.1); }
    .page-footer { margin-top: 64px; padding-top: 32px; border-top: 1px solid rgba(255,255,255,0.1); display: flex; flex-wrap: wrap; gap: 24px; justify-content: center; }
    .page-footer a { color: #9CA3AF; text-decoration: none; font-size: 14px; transition: color 0.2s; }
    .page-footer a:hover { color: #A78BFA; }
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
    <p>For questions about these terms, contact us at: support@symphonytek.dk</p>

    <p class="updated">Last updated: December 2024</p>
    <div class="page-footer">
      <a href="/">Home</a>
      <a href="/privacy">Privacy Policy</a>
      <a href="https://github.com/symphovais/hotkeypaster">GitHub</a>
    </div>
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
