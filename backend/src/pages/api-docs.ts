export const apiDocsPage = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Remote Control API - TalkKeys</title>
  <link rel="icon" type="image/png" href="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/icon-talkkeys.png">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0F0F1A; color: #E5E7EB; line-height: 1.7; }
    .header { background: linear-gradient(135deg, rgba(59, 130, 246, 0.3) 0%, rgba(99, 102, 241, 0.2) 100%); padding: 64px 24px; text-align: center; border-bottom: 1px solid rgba(255,255,255,0.1); }
    .header h1 { font-size: 36px; margin-bottom: 8px; background: linear-gradient(135deg, #fff 0%, #93C5FD 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .header p { color: #9CA3AF; }
    .badge { display: inline-block; background: #3B82F6; color: white; padding: 6px 14px; border-radius: 20px; font-size: 13px; font-weight: 600; margin-top: 16px; }
    .content { max-width: 900px; margin: 0 auto; padding: 64px 24px; }
    h2 { color: #E5E7EB; margin: 48px 0 16px; font-size: 24px; padding-bottom: 8px; border-bottom: 1px solid rgba(255,255,255,0.1); }
    h2:first-of-type { margin-top: 0; }
    h3 { color: #93C5FD; font-size: 18px; margin: 32px 0 12px; }
    p { margin-bottom: 16px; color: #9CA3AF; }
    code { background: rgba(59, 130, 246, 0.15); color: #93C5FD; padding: 2px 8px; border-radius: 4px; font-family: 'Monaco', 'Consolas', monospace; font-size: 14px; }
    pre { background: rgba(0,0,0,0.4); border: 1px solid rgba(255,255,255,0.1); border-radius: 12px; padding: 20px; overflow-x: auto; margin: 16px 0 24px; }
    pre code { background: none; padding: 0; color: #E5E7EB; }
    .endpoint { background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08); border-radius: 12px; padding: 24px; margin: 16px 0; }
    .endpoint-header { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; flex-wrap: wrap; }
    .method { padding: 4px 10px; border-radius: 6px; font-size: 12px; font-weight: 700; font-family: monospace; }
    .method-get { background: #059669; color: white; }
    .method-post { background: #3B82F6; color: white; }
    .path { font-family: monospace; font-size: 16px; color: #E5E7EB; font-weight: 600; }
    .endpoint-desc { color: #9CA3AF; font-size: 14px; }
    table { width: 100%; border-collapse: collapse; margin: 16px 0; }
    th, td { text-align: left; padding: 12px; border-bottom: 1px solid rgba(255,255,255,0.1); }
    th { color: #9CA3AF; font-size: 13px; font-weight: 600; text-transform: uppercase; }
    td { color: #E5E7EB; font-size: 14px; }
    td code { font-size: 13px; }
    .note { background: rgba(59, 130, 246, 0.1); border-left: 4px solid #3B82F6; padding: 16px 20px; border-radius: 0 8px 8px 0; margin: 24px 0; }
    .note strong { color: #93C5FD; }
    .page-footer { margin-top: 64px; padding-top: 32px; border-top: 1px solid rgba(255,255,255,0.1); display: flex; flex-wrap: wrap; gap: 24px; justify-content: center; }
    .page-footer a { color: #9CA3AF; text-decoration: none; font-size: 14px; transition: color 0.2s; }
    .page-footer a:hover { color: #93C5FD; }
    .json-key { color: #93C5FD; }
    .json-string { color: #A78BFA; }
    .json-bool { color: #F472B6; }
  </style>
</head>
<body>
  <div class="header">
    <h1>🔗 Remote Control API</h1>
    <p>Control TalkKeys from external applications</p>
    <div class="badge">http://localhost:38450</div>
  </div>
  <div class="content">
    <h2>Overview</h2>
    <p>TalkKeys v1.2.0 exposes a local HTTP API that allows external applications to control transcription. Perfect for hardware buttons (like Jabra headsets), AI assistants, or custom integrations.</p>

    <div class="note">
      <strong>Local Only:</strong> The API is only accessible from localhost for security. It runs automatically when TalkKeys starts.
    </div>

    <h2>Endpoints</h2>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-get">GET</span>
        <span class="path">/</span>
      </div>
      <p class="endpoint-desc">Get API capabilities, available features, and all endpoints.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">name</span>": <span class="json-string">"TalkKeys"</span>,
  "<span class="json-key">version</span>": <span class="json-string">"1.2.0"</span>,
  "<span class="json-key">capabilities</span>": [...],
  "<span class="json-key">endpoints</span>": [...]
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-get">GET</span>
        <span class="path">/status</span>
      </div>
      <p class="endpoint-desc">Get the current status of TalkKeys.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">status</span>": <span class="json-string">"idle"</span>,
  "<span class="json-key">recording</span>": <span class="json-bool">false</span>,
  "<span class="json-key">processing</span>": <span class="json-bool">false</span>,
  "<span class="json-key">authenticated</span>": <span class="json-bool">true</span>
}</code></pre>
      <p>Status values: <code>idle</code>, <code>recording</code>, <code>processing</code></p>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-post">POST</span>
        <span class="path">/starttranscription</span>
      </div>
      <p class="endpoint-desc">Start voice recording. Returns error if already recording.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">status</span>": <span class="json-string">"recording"</span>,
  "<span class="json-key">message</span>": <span class="json-string">"Recording started"</span>
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-post">POST</span>
        <span class="path">/stoptranscription</span>
      </div>
      <p class="endpoint-desc">Stop recording and transcribe the audio. Text is pasted to the active application.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">status</span>": <span class="json-string">"processing"</span>,
  "<span class="json-key">message</span>": <span class="json-string">"Processing transcription"</span>
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-post">POST</span>
        <span class="path">/canceltranscription</span>
      </div>
      <p class="endpoint-desc">Cancel the current recording without transcribing.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">status</span>": <span class="json-string">"idle"</span>,
  "<span class="json-key">message</span>": <span class="json-string">"Recording cancelled"</span>
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-post">POST</span>
        <span class="path">/explain</span>
      </div>
      <p class="endpoint-desc">Explain the currently selected text using AI (WTF feature). Result appears in a popup.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">message</span>": <span class="json-string">"Explanation requested"</span>
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-get">GET</span>
        <span class="path">/microphones</span>
      </div>
      <p class="endpoint-desc">List all available microphones.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">microphones</span>": [
    { "<span class="json-key">index</span>": 0, "<span class="json-key">name</span>": <span class="json-string">"Jabra Engage 50"</span>, "<span class="json-key">current</span>": <span class="json-bool">true</span> },
    { "<span class="json-key">index</span>": 1, "<span class="json-key">name</span>": <span class="json-string">"Realtek Audio"</span>, "<span class="json-key">current</span>": <span class="json-bool">false</span> }
  ]
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-post">POST</span>
        <span class="path">/microphone</span>
      </div>
      <p class="endpoint-desc">Set the active microphone by index.</p>
      <h4>Request Body</h4>
      <pre><code>{ "<span class="json-key">index</span>": 0 }</code></pre>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">message</span>": <span class="json-string">"Microphone set to: Jabra Engage 50"</span>
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-get">GET</span>
        <span class="path">/shortcuts</span>
      </div>
      <p class="endpoint-desc">Get currently configured keyboard shortcuts.</p>
      <h4>Response</h4>
      <pre><code>{
  "<span class="json-key">success</span>": <span class="json-bool">true</span>,
  "<span class="json-key">shortcuts</span>": {
    "<span class="json-key">transcription</span>": <span class="json-string">"Ctrl+Shift+Space"</span>,
    "<span class="json-key">explain</span>": <span class="json-string">"Ctrl+Win+E"</span>
  }
}</code></pre>
    </div>

    <div class="endpoint">
      <div class="endpoint-header">
        <span class="method method-post">POST</span>
        <span class="path">/shortcuts</span>
      </div>
      <p class="endpoint-desc">Update keyboard shortcuts.</p>
      <h4>Request Body</h4>
      <pre><code>{
  "<span class="json-key">shortcuts</span>": {
    "<span class="json-key">transcription</span>": <span class="json-string">"Ctrl+Alt+Space"</span>
  }
}</code></pre>
    </div>

    <h2>Quick Start Examples</h2>

    <h3>PowerShell</h3>
    <pre><code># Get status
Invoke-WebRequest -Uri "http://localhost:38450/status" | ConvertFrom-Json

# Start transcription
Invoke-WebRequest -Uri "http://localhost:38450/starttranscription" -Method POST

# Stop transcription
Invoke-WebRequest -Uri "http://localhost:38450/stoptranscription" -Method POST</code></pre>

    <h3>cURL</h3>
    <pre><code># Get status
curl http://localhost:38450/status

# Start transcription
curl -X POST http://localhost:38450/starttranscription

# Stop transcription
curl -X POST http://localhost:38450/stoptranscription</code></pre>

    <h3>JavaScript</h3>
    <pre><code>// Start transcription
await fetch('http://localhost:38450/starttranscription', { method: 'POST' });

// Stop transcription
await fetch('http://localhost:38450/stoptranscription', { method: 'POST' });</code></pre>

    <h2>Error Handling</h2>
    <table>
      <tr><th>Scenario</th><th>Response</th></tr>
      <tr><td>Already recording</td><td><code>{ "success": false, "message": "Already recording" }</code></td></tr>
      <tr><td>Not recording</td><td><code>{ "success": false, "message": "Not recording" }</code></td></tr>
      <tr><td>Not authenticated</td><td><code>{ "success": false, "message": "Not authenticated" }</code></td></tr>
      <tr><td>Invalid endpoint</td><td>404 Not Found</td></tr>
      <tr><td>Wrong HTTP method</td><td>405 Method Not Allowed</td></tr>
    </table>

    <div class="page-footer">
      <a href="/">Home</a>
      <a href="/releases">Release Notes</a>
      <a href="/privacy">Privacy Policy</a>
      <a href="/tos">Terms of Service</a>
      <a href="https://github.com/symphovais/hotkeypaster">GitHub</a>
    </div>
  </div>
</body>
</html>`;
