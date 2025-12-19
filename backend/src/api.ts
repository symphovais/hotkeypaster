import { Env, JWTPayload, ApiResponse, UsageInfo } from './types';
import { verifyJWT } from './jwt';
import { getUsageStats, incrementUsage, getUserById } from './db';

const GROQ_WHISPER_URL = 'https://api.groq.com/openai/v1/audio/transcriptions';
const GROQ_CHAT_URL = 'https://api.groq.com/openai/v1/chat/completions';

// CORS headers for desktop app
const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type, Authorization',
};

export function handleCors(): Response {
  return new Response(null, { headers: corsHeaders });
}

// Verify auth token and return user payload
export async function authenticate(request: Request, env: Env): Promise<JWTPayload | null> {
  const authHeader = request.headers.get('Authorization');
  if (!authHeader?.startsWith('Bearer ')) {
    return null;
  }

  const token = authHeader.slice(7);
  return verifyJWT(token, env.JWT_SECRET);
}

// JSON response helper
function jsonResponse<T>(data: ApiResponse<T>, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      'Content-Type': 'application/json',
      ...corsHeaders
    }
  });
}

// Error response helper
function errorResponse(message: string, status = 400): Response {
  return jsonResponse({ success: false, error: message }, status);
}

// Get usage stats endpoint
export async function handleGetUsage(
  request: Request,
  env: Env,
  user: JWTPayload
): Promise<Response> {
  const stats = await getUsageStats(env, user.sub);

  // Calculate reset time (midnight UTC)
  const now = new Date();
  const tomorrow = new Date(now);
  tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
  tomorrow.setUTCHours(0, 0, 0, 0);

  const data: UsageInfo = {
    used_seconds: stats.used,
    limit_seconds: stats.limit,
    remaining_seconds: stats.remaining,
    reset_at: tomorrow.toISOString()
  };

  return jsonResponse({ success: true, data });
}

// Estimate audio duration from file size (rough estimate)
// Typical audio: ~16KB per second for decent quality
function estimateAudioDuration(sizeBytes: number): number {
  return Math.ceil(sizeBytes / 16000);
}

// Proxy Whisper transcription to Groq
export async function handleWhisperProxy(
  request: Request,
  env: Env,
  user: JWTPayload
): Promise<Response> {
  // Check usage limits
  const stats = await getUsageStats(env, user.sub);
  if (stats.remaining <= 0) {
    return errorResponse(
      `Daily limit reached (${stats.limit / 60} minutes). Resets at midnight UTC.`,
      429
    );
  }

  // Get the audio file from the request
  const contentType = request.headers.get('Content-Type') || '';
  if (!contentType.includes('multipart/form-data')) {
    return errorResponse('Expected multipart/form-data with audio file');
  }

  let formData: FormData;
  try {
    formData = await request.formData();
  } catch (parseError) {
    console.error('Failed to parse FormData from client:', parseError);
    const msg = parseError instanceof Error ? parseError.message : 'Unknown parse error';
    return errorResponse(`Failed to parse upload: ${msg}`, 400);
  }

  try {
    const file = formData.get('file');

    if (!file || !(file instanceof File)) {
      return errorResponse('No audio file provided');
    }

    // Debug logging
    console.log('Received file:', {
      name: file.name,
      type: file.type,
      size: file.size
    });

    // Estimate duration for usage tracking
    const estimatedDuration = estimateAudioDuration(file.size);

    // Check if this request would exceed the limit
    if (stats.used + estimatedDuration > stats.limit) {
      const remainingMinutes = Math.floor(stats.remaining / 60);
      const remainingSeconds = stats.remaining % 60;
      return errorResponse(
        `Audio too long. You have ${remainingMinutes}m ${remainingSeconds}s remaining today.`,
        429
      );
    }

    // Build multipart body manually to ensure proper Content-Disposition headers
    const boundary = '----WebKitFormBoundary' + Math.random().toString(36).substring(2);
    const fileBuffer = await file.arrayBuffer();
    const fileName = file.name || 'audio.wav';
    const model = formData.get('model')?.toString() || 'whisper-large-v3-turbo';

    // File part - note: we'll handle the binary separately
    const fileHeader = [
      `--${boundary}`,
      `Content-Disposition: form-data; name="file"; filename="${fileName}"`,
      `Content-Type: ${file.type || 'audio/wav'}`,
      '',
      ''
    ].join('\r\n');

    // Model part
    const modelPart = [
      `--${boundary}`,
      'Content-Disposition: form-data; name="model"',
      '',
      model
    ].join('\r\n');

    // Optional language part
    const language = formData.get('language');
    let languagePart = '';
    if (language) {
      languagePart = [
        `--${boundary}`,
        'Content-Disposition: form-data; name="language"',
        '',
        language.toString()
      ].join('\r\n');
    }

    // Closing boundary
    const closingBoundary = `\r\n--${boundary}--\r\n`;

    // Combine all parts into a single ArrayBuffer
    const encoder = new TextEncoder();
    const fileHeaderBytes = encoder.encode(fileHeader);
    const modelPartBytes = encoder.encode('\r\n' + modelPart);
    const languagePartBytes = language ? encoder.encode('\r\n' + languagePart) : new Uint8Array(0);
    const closingBytes = encoder.encode(closingBoundary);
    const fileBytes = new Uint8Array(fileBuffer);

    // Calculate total length and create combined buffer
    const totalLength = fileHeaderBytes.length + fileBytes.length + modelPartBytes.length +
                        languagePartBytes.length + closingBytes.length;
    const body = new Uint8Array(totalLength);

    let offset = 0;
    body.set(fileHeaderBytes, offset); offset += fileHeaderBytes.length;
    body.set(fileBytes, offset); offset += fileBytes.length;
    body.set(modelPartBytes, offset); offset += modelPartBytes.length;
    body.set(languagePartBytes, offset); offset += languagePartBytes.length;
    body.set(closingBytes, offset);

    console.log('Sending to Groq with boundary:', boundary, 'body size:', body.length);

    // Proxy to Groq
    const groqResponse = await fetch(GROQ_WHISPER_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${env.GROQ_API_KEY}`,
        'Content-Type': `multipart/form-data; boundary=${boundary}`
      },
      body: body
    });

    if (!groqResponse.ok) {
      const error = await groqResponse.text();
      console.error('Groq Whisper error:', groqResponse.status, error);
      return errorResponse(`Groq API error (${groqResponse.status}): ${error.substring(0, 200)}`, 502);
    }

    // Track usage after successful transcription
    await incrementUsage(env, user.sub, estimatedDuration);

    // Return Groq's response
    const result = await groqResponse.json();
    return jsonResponse({ success: true, data: result });

  } catch (err) {
    console.error('Whisper proxy error:', err);
    const message = err instanceof Error ? err.message : 'Unknown error';
    return errorResponse(`Transcription error: ${message}`, 500);
  }
}

// Proxy text cleaning to Groq
export async function handleCleanProxy(
  request: Request,
  env: Env,
  user: JWTPayload
): Promise<Response> {
  // Text cleaning doesn't count against audio limit (it's cheap)
  // but we still require authentication

  try {
    const body = await request.json() as {
      text: string;
      context?: string;
      wordsList?: string[];
    };

    if (!body.text) {
      return errorResponse('No text provided');
    }

    // Build words list section if provided
    let wordsSection = '';
    if (body.wordsList && body.wordsList.length > 0) {
      wordsSection = `\n\nWORDS LIST - The user frequently uses these words/phrases. The speech-to-text may have misheard them as similar-sounding words. Analyze the text and replace any misheard variations with the correct spelling from this list:\n${body.wordsList.join(', ')}`;
    }

    // Build cleanup request
    const systemPrompt = `You are a text cleaning assistant for a voice-to-text transcription application called TalkKeys.
The user speaks into their microphone, and the audio is transcribed to text. Your job is to clean up the raw transcription.

RULES:
1. ONLY fix issues - do NOT add new content or information that wasn't spoken
2. Remove filler words (um, uh, like, you know, I mean, sort of, kind of)
3. Fix grammar errors and add proper punctuation
4. Ensure proper capitalization
5. Format lists and structure when appropriate
6. NEVER explain what you did - just output the cleaned text
7. If the input is empty or just noise, output nothing
${wordsSection}
${body.context ? `\nCONTEXT: The user is typing in: ${body.context}` : ''}

Output ONLY the cleaned text, nothing else.`;

    const groqBody = {
      model: 'openai/gpt-oss-20b',
      messages: [
        { role: 'system', content: systemPrompt },
        { role: 'user', content: `Clean this transcription:\n\n${body.text}` }
      ],
      temperature: 0.3,
      max_tokens: 500,
      stream: false
    };

    const groqResponse = await fetch(GROQ_CHAT_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${env.GROQ_API_KEY}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(groqBody)
    });

    if (!groqResponse.ok) {
      const error = await groqResponse.text();
      console.error('Groq Chat error:', error);
      return errorResponse(`Text cleaning failed: ${groqResponse.status}`, 502);
    }

    const result = await groqResponse.json() as {
      choices: Array<{ message: { content: string } }>;
    };

    const cleanedText = result.choices?.[0]?.message?.content || body.text;

    return jsonResponse({
      success: true,
      data: { cleaned_text: cleanedText }
    });

  } catch (err) {
    console.error('Clean proxy error:', err);
    const message = err instanceof Error ? err.message : 'Unknown error';
    return errorResponse(`Cleaning error: ${message}`, 500);
  }
}

// Proxy text explanation to Groq (Plain English Explainer)
export async function handleExplainProxy(
  request: Request,
  env: Env,
  user: JWTPayload
): Promise<Response> {
  try {
    const body = await request.json() as {
      text: string;
    };

    if (!body.text) {
      return errorResponse('No text provided');
    }

    // Limit text length to prevent abuse
    if (body.text.length > 2000) {
      return errorResponse('Text too long (max 2000 characters)');
    }

    const systemPrompt = `You're a sarcastic translator who turns corporate nonsense into brutally honest truth bombs.

Your job: Decode what this text ACTUALLY means. Be witty. Be savage. Make them laugh (or cry).

Style:
- Channel your inner cynical coworker who's seen it all
- Dry humor > crude humor. Wit > vulgarity.
- If it's passive-aggressive, roast it
- If it's corporate fluff, deflate it
- One punchy sentence. 15 words MAX.
- Don't explain - just deliver the truth like a punchline

Examples:
"We need to align on the go-forward strategy" → "Translation: Let's schedule a meeting about scheduling meetings"
"Per my last email" → "I'm barely containing my rage right now"
"Let's take this offline" → "This meeting has witnesses"
"We're pivoting to focus on core competencies" → "The experiment failed spectacularly"
"I'll loop you in" → "I'll forget to CC you"
"That's an interesting perspective" → "That's the dumbest thing I've heard today"
"We should probably sync up" → "I need something from you"
"Thanks for your patience" → "Sorry we suck"
"Just to clarify" → "You got it completely wrong"`;

    const groqBody = {
      model: 'openai/gpt-oss-20b',  // Use same model as text cleaning
      messages: [
        { role: 'system', content: systemPrompt },
        { role: 'user', content: body.text }
      ],
      temperature: 0.7,
      max_tokens: 100,
      stream: false
    };

    console.log('Explain request:', { textLength: body.text.length });

    const groqResponse = await fetch(GROQ_CHAT_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${env.GROQ_API_KEY}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(groqBody)
    });

    if (!groqResponse.ok) {
      const error = await groqResponse.text();
      console.error('Groq Chat error (explain):', groqResponse.status, error);
      return errorResponse(`Explanation failed: ${groqResponse.status}`, 502);
    }

    const result = await groqResponse.json() as {
      choices: Array<{ message: { content: string } }>;
    };

    console.log('Groq explain response:', JSON.stringify(result).substring(0, 200));

    const explanation = result.choices?.[0]?.message?.content?.trim();

    if (!explanation) {
      console.error('No explanation in Groq response:', result);
      return errorResponse('No explanation generated', 500);
    }

    return jsonResponse({
      success: true,
      data: { explanation }
    });

  } catch (err) {
    console.error('Explain proxy error:', err);
    const message = err instanceof Error ? err.message : 'Unknown error';
    return errorResponse(`Explanation error: ${message}`, 500);
  }
}

// Analyze transcription history for word suggestions
export async function handleAnalyzeWordsProxy(
  request: Request,
  env: Env,
  user: JWTPayload
): Promise<Response> {
  try {
    const body = await request.json() as {
      transcriptions: Array<{ raw: string; cleaned: string }>;
      existingWords?: string[];
    };

    if (!body.transcriptions || body.transcriptions.length === 0) {
      return errorResponse('No transcriptions provided');
    }

    // Limit to 50 transcriptions to prevent abuse
    const transcriptions = body.transcriptions.slice(0, 50);

    // Build transcription pairs for analysis
    const transcriptionPairs = transcriptions
      .map((t, i) => `#${i + 1}\nRaw: ${t.raw}\nCleaned: ${t.cleaned}`)
      .join('\n\n');

    const existingWordsNote = body.existingWords && body.existingWords.length > 0
      ? `\n\nThe user already has these words in their list (don't suggest these again):\n${body.existingWords.join(', ')}`
      : '';

    const systemPrompt = `Analyze these voice transcriptions to identify words that may need correct spellings added to the user's words list.

For each transcription, you're given:
- Raw: What Whisper heard (speech-to-text result)
- Cleaned: What the AI cleaned it to

Look for:
1. Proper nouns that might be spelled inconsistently (company names, people, products)
2. Technical terms that could be misheard (programming terms, acronyms)
3. Words that don't quite make sense in context and might be mishearings
4. Names or terms that appear multiple times with different spellings
5. Domain-specific terminology the user frequently uses${existingWordsNote}

Return a JSON array of correctly-spelled words the user should add:
["Claude Code", "Anthropic", "Kubernetes"]

IMPORTANT:
- Only suggest words you're confident the user intended to say
- Return the CORRECT spelling (what they meant, not what was transcribed)
- Return an empty array [] if no issues found
- Maximum 10 suggestions per analysis
- Output ONLY the JSON array, nothing else`;

    const groqBody = {
      model: 'openai/gpt-oss-20b',
      messages: [
        { role: 'system', content: systemPrompt },
        { role: 'user', content: `TRANSCRIPTIONS:\n\n${transcriptionPairs}` }
      ],
      temperature: 0.3,
      max_tokens: 500,
      stream: false
    };

    console.log('Analyze words request:', { transcriptionCount: transcriptions.length });

    const groqResponse = await fetch(GROQ_CHAT_URL, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${env.GROQ_API_KEY}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(groqBody)
    });

    if (!groqResponse.ok) {
      const error = await groqResponse.text();
      console.error('Groq Chat error (analyze-words):', groqResponse.status, error);
      return errorResponse(`Analysis failed: ${groqResponse.status}`, 502);
    }

    const result = await groqResponse.json() as {
      choices: Array<{ message: { content: string } }>;
    };

    const content = result.choices?.[0]?.message?.content?.trim() || '[]';

    // Extract JSON array from response (handle potential markdown code blocks)
    let suggestions: string[] = [];
    try {
      let jsonContent = content;
      // Remove markdown code blocks if present
      if (jsonContent.includes('```')) {
        const start = jsonContent.indexOf('[');
        const end = jsonContent.lastIndexOf(']');
        if (start >= 0 && end > start) {
          jsonContent = jsonContent.substring(start, end + 1);
        }
      }
      // Ensure we have a valid JSON array
      if (!jsonContent.startsWith('[')) {
        const start = jsonContent.indexOf('[');
        if (start >= 0) jsonContent = jsonContent.substring(start);
      }
      if (!jsonContent.endsWith(']')) {
        const end = jsonContent.lastIndexOf(']');
        if (end >= 0) jsonContent = jsonContent.substring(0, end + 1);
      }

      suggestions = JSON.parse(jsonContent);
      // Limit to 10 suggestions
      suggestions = suggestions.slice(0, 10);
    } catch (parseError) {
      console.error('Failed to parse suggestions:', content);
      suggestions = [];
    }

    console.log('Analyze words response:', { suggestionsCount: suggestions.length });

    return jsonResponse({
      success: true,
      data: { suggestions }
    });

  } catch (err) {
    console.error('Analyze words proxy error:', err);
    const message = err instanceof Error ? err.message : 'Unknown error';
    return errorResponse(`Analysis error: ${message}`, 500);
  }
}

// Get user profile
export async function handleGetProfile(
  request: Request,
  env: Env,
  user: JWTPayload
): Promise<Response> {
  const dbUser = await getUserById(env, user.sub);

  if (!dbUser) {
    return errorResponse('User not found', 404);
  }

  return jsonResponse({
    success: true,
    data: {
      id: dbUser.id,
      email: dbUser.email,
      name: dbUser.name,
      created_at: new Date(dbUser.created_at * 1000).toISOString()
    }
  });
}
