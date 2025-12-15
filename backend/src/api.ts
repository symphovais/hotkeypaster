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
    };

    if (!body.text) {
      return errorResponse('No text provided');
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

${body.context ? `CONTEXT: The user is typing in: ${body.context}` : ''}

Output ONLY the cleaned text, nothing else.`;

    const groqBody = {
      model: 'llama-3.1-8b-instant',
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

    const systemPrompt = `You're a brutally honest translator of bullshit.

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
"I'll take that under advisement" → "No"`;

    const groqBody = {
      model: 'llama-3.1-8b-instant',  // Use same model as text cleaning
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
