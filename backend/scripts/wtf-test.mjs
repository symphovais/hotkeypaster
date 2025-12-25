#!/usr/bin/env node
/**
 * WTF Feature Test Harness
 *
 * Tests different Groq models and prompts for the WTF (What are the Facts) feature.
 * Shows side-by-side outputs for quality comparison.
 *
 * Usage:
 *   node wtf-test.mjs                    # Test with default phrases
 *   node wtf-test.mjs "custom phrase"    # Test with custom phrase
 *   node wtf-test.mjs --models           # List available models
 *   node wtf-test.mjs --help             # Show help
 */

import { readFileSync, existsSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// ============================================================================
// CONFIGURATION - Edit these to customize tests
// ============================================================================

const MODELS = [
  'llama-3.3-70b-versatile',                     // Llama 3.3 - 280 tok/s
];

const PROMPTS = {
  wtf: {
    name: 'WTF',
    system: `You're a brutally honest translator of bullshit.

Your job: Tell the user what this text ACTUALLY means in 10 words or less.

Rules:
- Cut through corporate speak, jargon, and fluff
- Be blunt. Be direct. No softening.
- If someone's being passive-aggressive, call it out
- If it's bad news wrapped in nice words, unwrap it
- Match the energy: if text is hostile, your translation can be too
- Never start with "This means" or "They're saying" - just say it
- One sentence max. Shorter is better.

Examples:
"We need to align on the go-forward strategy" → "Let's have another pointless meeting"
"Per my last email" → "I already told you this, read your damn inbox"
"We're pivoting to focus on core competencies" → "We failed, back to basics"
"I'll take that under advisement" → "No"
"Thanks for your patience" → "Sorry I'm slow"
"Let's circle back" → "I'm avoiding this"`
  },

  plain: {
    name: 'Plain',
    system: `You decode text to reveal its actual meaning. No emotion, just facts.

Your job: State what this text actually means in plain, neutral language.

Rules:
- Be direct and neutral, no emotional charge
- State ONLY what the person actually means
- Remove all fluff, keep the substance
- Don't be mean, don't be funny, just be accurate
- One clear sentence. 15 words MAX.

Examples:
"We need to align on the go-forward strategy" → "We need to agree on the plan"
"Per my last email" → "I mentioned this before"
"Thanks for your patience" → "Sorry for the delay"
"Let's circle back" → "I'll address this later"
"I'll take that under advisement" → "I'll consider it but probably won't do it"
"Let's take this offline" → "Let's discuss this privately"`
  }
};

const DEFAULT_PHRASES = [
  "Let's circle back on this",
  "We need to align on the go-forward strategy",
  "Per my last email",
  "Thanks for your patience",
  "I'll take this offline",
  "That's an interesting perspective",
  "We should probably sync up",
];

// ============================================================================
// API CONFIGURATION
// ============================================================================

const GROQ_API_URL = 'https://api.groq.com/openai/v1/chat/completions';

function getApiKey() {
  // Try environment variable first
  if (process.env.GROQ_API_KEY) {
    return process.env.GROQ_API_KEY;
  }

  // Try .dev.vars file
  const devVarsPath = join(__dirname, '..', '.dev.vars');
  if (existsSync(devVarsPath)) {
    const content = readFileSync(devVarsPath, 'utf-8');
    const match = content.match(/GROQ_API_KEY=(.+)/);
    if (match) {
      return match[1].trim();
    }
  }

  console.error('\x1b[31mError: GROQ_API_KEY not found\x1b[0m');
  console.error('\nSet it via:');
  console.error('  1. Environment variable: set GROQ_API_KEY=gsk_...');
  console.error('  2. Create backend/.dev.vars with: GROQ_API_KEY=gsk_...');
  process.exit(1);
}

// ============================================================================
// API CALL
// ============================================================================

async function callGroq(model, systemPrompt, userText, apiKey) {
  const start = Date.now();

  try {
    // Build request body
    const body = {
      model,
      messages: [
        { role: 'system', content: systemPrompt },
        { role: 'user', content: userText }
      ],
      temperature: 0.7,
      max_tokens: 500,
      stream: false
    };

    // For Qwen models, hide reasoning output
    if (model.includes('qwen')) {
      body.reasoning_format = 'hidden';
    }

    const response = await fetch(GROQ_API_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(body)
    });

    const elapsed = Date.now() - start;

    if (!response.ok) {
      const error = await response.text();
      return { error: `HTTP ${response.status}: ${error.substring(0, 100)}`, elapsed };
    }

    const result = await response.json();
    const content = result.choices?.[0]?.message?.content?.trim();

    return {
      response: content || '(empty response)',
      elapsed,
      tokens: result.usage
    };
  } catch (err) {
    return { error: err.message, elapsed: Date.now() - start };
  }
}

// ============================================================================
// OUTPUT FORMATTING
// ============================================================================

const colors = {
  reset: '\x1b[0m',
  bold: '\x1b[1m',
  dim: '\x1b[2m',
  cyan: '\x1b[36m',
  yellow: '\x1b[33m',
  green: '\x1b[32m',
  red: '\x1b[31m',
  magenta: '\x1b[35m',
  blue: '\x1b[34m',
};

function formatTime(ms) {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function truncate(str, maxLen) {
  if (str.length <= maxLen) return str;
  return str.substring(0, maxLen - 3) + '...';
}

function printHeader(text) {
  console.log('\n' + colors.cyan + colors.bold + '═'.repeat(80) + colors.reset);
  console.log(colors.cyan + colors.bold + ' ' + text + colors.reset);
  console.log(colors.cyan + '═'.repeat(80) + colors.reset);
}

function printPhrase(phrase) {
  console.log('\n' + colors.yellow + colors.bold + '📝 Test Phrase: ' + colors.reset + `"${phrase}"`);
  console.log(colors.dim + '─'.repeat(80) + colors.reset);
}

function printResult(model, promptName, result) {
  const modelShort = model.replace('openai/', '').replace('-versatile', '');
  const timeStr = formatTime(result.elapsed);
  const timeColor = result.elapsed < 500 ? colors.green :
                    result.elapsed < 1500 ? colors.yellow : colors.red;

  if (result.error) {
    console.log(
      colors.magenta + `[${modelShort}]` + colors.reset +
      colors.dim + ` (${promptName})` + colors.reset +
      timeColor + ` ${timeStr}` + colors.reset
    );
    console.log(colors.red + '   ✗ ' + result.error + colors.reset);
  } else {
    console.log(
      colors.magenta + `[${modelShort}]` + colors.reset +
      colors.dim + ` (${promptName})` + colors.reset +
      timeColor + ` ${timeStr}` + colors.reset
    );
    console.log(colors.green + '   → ' + colors.reset + result.response);
  }
}

// ============================================================================
// TEST RUNNER
// ============================================================================

async function runTests(phrases, models, prompts) {
  const apiKey = getApiKey();

  console.log(colors.bold + '\n🧪 WTF Feature Test Harness' + colors.reset);
  console.log(colors.dim + `Testing ${models.length} models × ${Object.keys(prompts).length} prompts × ${phrases.length} phrases` + colors.reset);

  for (const phrase of phrases) {
    printPhrase(phrase);

    for (const [promptKey, promptConfig] of Object.entries(prompts)) {
      for (const model of models) {
        const result = await callGroq(model, promptConfig.system, phrase, apiKey);
        printResult(model, promptConfig.name, result);
      }
      if (Object.keys(prompts).length > 1) {
        console.log(colors.dim + '  ·  ·  ·' + colors.reset);
      }
    }
  }

  console.log('\n' + colors.green + '✓ Tests complete' + colors.reset + '\n');
}

// ============================================================================
// INTERACTIVE MODE - Edit prompts/models on the fly
// ============================================================================

async function interactiveTest(apiKey) {
  const readline = await import('readline');
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
  });

  const question = (q) => new Promise(resolve => rl.question(q, resolve));

  console.log(colors.bold + '\n🔬 Interactive WTF Test Mode' + colors.reset);
  console.log(colors.dim + 'Type a phrase to test, or commands: /models, /prompts, /quit' + colors.reset);

  let currentModel = MODELS[0];
  let currentPromptKey = 'current';

  while (true) {
    const input = await question(colors.cyan + '\n> ' + colors.reset);

    if (input === '/quit' || input === '/q') break;

    if (input === '/models') {
      console.log('\nAvailable models:');
      MODELS.forEach((m, i) => {
        const marker = m === currentModel ? colors.green + ' ← current' + colors.reset : '';
        console.log(`  ${i + 1}. ${m}${marker}`);
      });
      const choice = await question('Select model (1-' + MODELS.length + '): ');
      const idx = parseInt(choice) - 1;
      if (idx >= 0 && idx < MODELS.length) {
        currentModel = MODELS[idx];
        console.log(colors.green + `Switched to ${currentModel}` + colors.reset);
      }
      continue;
    }

    if (input === '/prompts') {
      console.log('\nAvailable prompts:');
      Object.entries(PROMPTS).forEach(([key, p], i) => {
        const marker = key === currentPromptKey ? colors.green + ' ← current' + colors.reset : '';
        console.log(`  ${i + 1}. ${p.name}${marker}`);
      });
      const choice = await question('Select prompt (1-' + Object.keys(PROMPTS).length + '): ');
      const keys = Object.keys(PROMPTS);
      const idx = parseInt(choice) - 1;
      if (idx >= 0 && idx < keys.length) {
        currentPromptKey = keys[idx];
        console.log(colors.green + `Switched to ${PROMPTS[currentPromptKey].name}` + colors.reset);
      }
      continue;
    }

    if (input.trim()) {
      const result = await callGroq(
        currentModel,
        PROMPTS[currentPromptKey].system,
        input,
        apiKey
      );
      printResult(currentModel, PROMPTS[currentPromptKey].name, result);
    }
  }

  rl.close();
  console.log('\n' + colors.dim + 'Goodbye!' + colors.reset + '\n');
}

// ============================================================================
// MAIN
// ============================================================================

const args = process.argv.slice(2);

if (args.includes('--help') || args.includes('-h')) {
  console.log(`
${colors.bold}WTF Feature Test Harness${colors.reset}

${colors.cyan}Usage:${colors.reset}
  node wtf-test.mjs                     Run with default test phrases
  node wtf-test.mjs "custom phrase"     Test a specific phrase
  node wtf-test.mjs -i                  Interactive mode
  node wtf-test.mjs --models            List available models
  node wtf-test.mjs --prompts           List available prompts

${colors.cyan}Options:${colors.reset}
  -i, --interactive     Interactive testing mode
  --models              Show available models
  --prompts             Show available prompts
  -h, --help            Show this help

${colors.cyan}Environment:${colors.reset}
  GROQ_API_KEY          Groq API key (or set in .dev.vars)
`);
  process.exit(0);
}

if (args.includes('--models')) {
  console.log('\nAvailable models:');
  MODELS.forEach(m => console.log(`  - ${m}`));
  console.log('');
  process.exit(0);
}

if (args.includes('--prompts')) {
  console.log('\nAvailable prompts:');
  Object.entries(PROMPTS).forEach(([key, p]) => {
    console.log(`\n${colors.bold}${key}${colors.reset} - ${p.name}`);
    console.log(colors.dim + p.system.substring(0, 200) + '...' + colors.reset);
  });
  console.log('');
  process.exit(0);
}

if (args.includes('-i') || args.includes('--interactive')) {
  const apiKey = getApiKey();
  interactiveTest(apiKey);
} else {
  // Get phrases from args or use defaults
  const customPhrases = args.filter(a => !a.startsWith('-'));
  const phrases = customPhrases.length > 0 ? customPhrases : DEFAULT_PHRASES;

  runTests(phrases, MODELS, PROMPTS);
}
