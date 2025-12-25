#!/usr/bin/env node
import { readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const GROQ_API_URL = 'https://api.groq.com/openai/v1/chat/completions';

// Read API key
const devVars = readFileSync(join(__dirname, '..', '.dev.vars'), 'utf-8');
const apiKey = devVars.match(/GROQ_API_KEY=(.+)/)[1].trim();

const savagePrompt = `The WTF Explainer: Savage Edition

You translate corporate bullshit into brutal truth. 10 WORDS OR LESS. No exceptions.

Rules:
- MAXIMUM 10 WORDS. Count them. Shorter is better.
- Expose the selfish motive behind the words
- Be blunt, cynical, clinical. Use words like: stalling, lazy, ego, control, fraud, cheap
- Never start with "This means" or "The speaker is"
- One sentence. No quotes around your answer.

Examples:
"We need to align on the go-forward strategy." → Let's have another pointless meeting.
"I suggest we spend all energy on Claude because it's the best." → I'm a fanboy; stop questioning me.
"We'll address IT blockers once we have concrete data." → Stop whining and use my tool.
"I'm just playing devil's advocate here." → I enjoy being a contrarian jerk.
"We are re-evaluating our headcount to optimize for growth." → Firing you to save my bonus.
"Per my last email" → Read your inbox, idiot.
"Thanks for your patience" → Sorry I'm slow.`;

const phrases = [
  "Let's circle back on this",
  "Per my last email",
  "Thanks for your patience",
  "I'll take that under advisement",
  "We should probably sync up",
  "That's an interesting perspective",
  "I wanted to loop you in on this",
  "Let's take this offline"
];

const models = ['llama-3.1-8b-instant'];

async function test(model, phrase) {
  const start = Date.now();
  try {
    const res = await fetch(GROQ_API_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model,
        messages: [
          { role: 'system', content: savagePrompt },
          { role: 'user', content: phrase }
        ],
        temperature: 0.7,
        max_tokens: 100
      })
    });
    const data = await res.json();
    const elapsed = Date.now() - start;
    return { result: data.choices?.[0]?.message?.content?.trim() || 'ERROR', elapsed };
  } catch (err) {
    return { result: `ERROR: ${err.message}`, elapsed: Date.now() - start };
  }
}

console.log('\n🔥 Testing Savage Edition Prompt\n');

for (const phrase of phrases) {
  console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
  console.log(`📝 "${phrase}"`);
  console.log('───────────────────────────────────────────────────');

  for (const model of models) {
    const { result, elapsed } = await test(model, phrase);
    const modelShort = model.replace('-versatile', '').replace('-instant', '');
    const timeColor = elapsed < 500 ? '\x1b[32m' : elapsed < 1500 ? '\x1b[33m' : '\x1b[31m';
    console.log(`\x1b[35m[${modelShort}]\x1b[0m ${timeColor}${elapsed}ms\x1b[0m → ${result}`);
  }
}

console.log('\n✓ Done\n');
