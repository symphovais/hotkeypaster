import { aboutContent } from '../content/about';

export function generateReleasesPage(): string {
  const releaseCards = aboutContent.releases.map((release, index) => {
    // Build features from slides (excluding Get Started slides)
    const features = release.slides
      .filter(slide => !slide.isGetStarted)
      .map(slide => {
        const items: string[] = [];
        // Note: badge is only on heroFeatures, not slides
        if (slide.highlights) {
          slide.highlights.forEach(h => {
            items.push(`<li>${h.text}</li>`);
          });
        }
        if (!slide.highlights) {
          items.push(`<li><strong>${slide.title}</strong> - ${slide.description}</li>`);
        }
        return items;
      })
      .flat();

    const isLatest = index === 0;
    const tagHtml = isLatest ? '<span class="tag new">Latest</span>' : '';

    return `
    <div class="release">
      <div class="release-header">
        <span class="version">v${release.version}</span>
        ${tagHtml}
        <span class="date">December 2024</span>
      </div>
      <h3>${release.title}</h3>
      <ul>
        ${features.join('\n        ')}
      </ul>
    </div>`;
  }).join('\n');

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Release Notes - TalkKeys</title>
  <link rel="icon" type="image/png" href="https://raw.githubusercontent.com/symphovais/hotkeypaster/master/icon-talkkeys.png">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0F0F1A; color: #E5E7EB; line-height: 1.7; }
    .header { background: linear-gradient(135deg, rgba(124, 58, 237, 0.3) 0%, rgba(99, 102, 241, 0.2) 100%); padding: 64px 24px; text-align: center; border-bottom: 1px solid rgba(255,255,255,0.1); }
    .header h1 { font-size: 36px; margin-bottom: 8px; background: linear-gradient(135deg, #fff 0%, #a5b4fc 100%); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .header p { color: #9CA3AF; }
    .content { max-width: 800px; margin: 0 auto; padding: 64px 24px; }
    .release { margin-bottom: 32px; background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08); border-radius: 16px; padding: 32px; transition: all 0.3s ease; }
    .release:hover { border-color: rgba(124, 58, 237, 0.3); transform: translateY(-2px); }
    .release-header { display: flex; align-items: center; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
    .version { font-size: 24px; font-weight: 700; color: #E5E7EB; }
    .date { color: #6B7280; font-size: 14px; }
    .tag { background: linear-gradient(135deg, #7C3AED 0%, #6366F1 100%); color: white; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }
    .tag.new { background: linear-gradient(135deg, #059669 0%, #10B981 100%); }
    h3 { color: #A78BFA; font-size: 18px; margin: 0 0 16px; font-weight: 600; }
    ul { margin: 0 0 0 20px; color: #9CA3AF; }
    li { margin-bottom: 10px; line-height: 1.6; }
    li strong { color: #E5E7EB; }
    .page-footer { margin-top: 64px; padding-top: 32px; border-top: 1px solid rgba(255,255,255,0.1); display: flex; flex-wrap: wrap; gap: 24px; justify-content: center; }
    .page-footer a { color: #9CA3AF; text-decoration: none; font-size: 14px; transition: color 0.2s; }
    .page-footer a:hover { color: #A78BFA; }
  </style>
</head>
<body>
  <div class="header">
    <h1>Release Notes</h1>
    <p>What's new in TalkKeys</p>
  </div>
  <div class="content">
    ${releaseCards}
    <div class="page-footer">
      <a href="/">Home</a>
      <a href="/privacy">Privacy Policy</a>
      <a href="/tos">Terms of Service</a>
      <a href="https://github.com/symphovais/hotkeypaster">GitHub</a>
    </div>
  </div>
</body>
</html>`;
}
