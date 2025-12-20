export const privacyPage = `<!DOCTYPE html>
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
      <a href="/releases">Release Notes</a>
      <a href="/tos">Terms of Service</a>
      <a href="https://github.com/symphovais/hotkeypaster">GitHub</a>
    </div>
  </div>
</body>
</html>`;
