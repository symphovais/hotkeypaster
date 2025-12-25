#!/usr/bin/env node
/**
 * WTF Prompt Tester
 *
 * Reads prompts from wtf-prompts.json and test phrases from test-inputs.json
 * Tests each phrase against each prompt and displays results.
 *
 * Usage:
 *   node run-wtf-tests.mjs              # Test all prompts with all phrases
 *   node run-wtf-tests.mjs --prompt v1  # Test only a specific prompt
 */

import { readFileSync, existsSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const GROQ_API_URL = 'https://api.groq.com/openai/v1/chat/completions';
const MODEL = 'llama-3.1-8b-instant';

// Colors for terminal output
const c = {
  reset: '\x1b[0m',
  bold: '\x1b[1m',
  dim: '\x1b[2m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  magenta: '\x1b[35m',
  cyan: '\x1b[36m',
};

// Load API key
function getApiKey() {
  if (process.env.GROQ_API_KEY) return process.env.GROQ_API_KEY;

  const devVarsPath = join(__dirname, '..', '..', '.dev.vars');
  if (existsSync(devVarsPath)) {
    const content = readFileSync(devVarsPath, 'utf-8');
    const match = content.match(/GROQ_API_KEY=(.+)/);
    if (match) return match[1].trim();
  }

  console.error(`${c.red}Error: GROQ_API_KEY not found${c.reset}`);
  console.error('Set via: GROQ_API_KEY=gsk_... or in backend/.dev.vars');
  process.exit(1);
}

// Load JSON files
function loadJson(filename) {
  const path = join(__dirname, filename);
  if (!existsSync(path)) {
    console.error(`${c.red}Error: ${filename} not found${c.reset}`);
    process.exit(1);
  }
  return JSON.parse(readFileSync(path, 'utf-8'));
}

// Call Groq API
async function callGroq(systemPrompt, userText, apiKey) {
  const start = Date.now();

  try {
    const response = await fetch(GROQ_API_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${apiKey}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        model: MODEL,
        messages: [
          { role: 'system', content: systemPrompt },
          { role: 'user', content: userText }
        ],
        temperature: 0.7,
        max_tokens: 100
      })
    });

    const elapsed = Date.now() - start;

    if (!response.ok) {
      const error = await response.text();
      return { error: `HTTP ${response.status}`, elapsed };
    }

    const result = await response.json();
    const content = result.choices?.[0]?.message?.content?.trim();

    return { response: content || '(empty)', elapsed };
  } catch (err) {
    return { error: err.message, elapsed: Date.now() - start };
  }
}

// Count words in a string
function countWords(str) {
  return str.split(/\s+/).filter(w => w.length > 0).length;
}

// Main test runner
async function main() {
  const apiKey = getApiKey();
  const inputs = loadJson('test-inputs.json');
  const promptsData = loadJson('wtf-prompts.json');

  const args = process.argv.slice(2);
  const promptFilter = args.includes('--prompt')
    ? args[args.indexOf('--prompt') + 1]
    : null;

  // Filter prompts if specified
  let prompts = promptsData.prompts;
  if (promptFilter) {
    if (!prompts[promptFilter]) {
      console.error(`${c.red}Prompt "${promptFilter}" not found${c.reset}`);
      console.log('Available:', Object.keys(prompts).join(', '));
      process.exit(1);
    }
    prompts = { [promptFilter]: prompts[promptFilter] };
  }

  console.log(`\n${c.bold}${c.cyan}WTF Prompt Tester${c.reset}`);
  console.log(`${c.dim}Model: ${MODEL}${c.reset}`);
  console.log(`${c.dim}Prompts: ${Object.keys(prompts).join(', ')}${c.reset}`);
  console.log(`${c.dim}Phrases: ${inputs.phrases.length}${c.reset}\n`);

  for (const phrase of inputs.phrases) {
    console.log(`${c.yellow}${c.bold}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${c.reset}`);
    console.log(`${c.yellow}📝 "${phrase}"${c.reset}`);
    console.log(`${c.dim}────────────────────────────────────────────────────────────${c.reset}`);

    for (const [key, prompt] of Object.entries(prompts)) {
      const result = await callGroq(prompt.system, phrase, apiKey);

      const timeColor = result.elapsed < 300 ? c.green : result.elapsed < 800 ? c.yellow : c.red;
      const timeStr = `${result.elapsed}ms`;

      if (result.error) {
        console.log(`${c.magenta}[${prompt.name}]${c.reset} ${timeColor}${timeStr}${c.reset}`);
        console.log(`  ${c.red}✗ ${result.error}${c.reset}`);
      } else {
        const wordCount = countWords(result.response);
        const countColor = wordCount <= 10 ? c.green : c.red;

        console.log(`${c.magenta}[${prompt.name}]${c.reset} ${timeColor}${timeStr}${c.reset} ${countColor}(${wordCount} words)${c.reset}`);
        console.log(`  ${c.green}→${c.reset} ${result.response}`);
      }
    }
    console.log('');
  }

  console.log(`${c.green}✓ Done${c.reset}\n`);
}

main().catch(console.error);
