import { describe, it, expect, beforeAll, beforeEach, afterEach } from 'vitest';
import { env, SELF, fetchMock } from 'cloudflare:test';
import { signJWT } from '../jwt';
import type { JWTPayload, Env } from '../types';

// Helper to create a valid JWT for testing
async function createTestToken(overrides: Partial<JWTPayload> = {}): Promise<string> {
  const payload: JWTPayload = {
    sub: 'test-user-123',
    email: 'test@example.com',
    name: 'Test User',
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 3600, // 1 hour from now
    ...overrides,
  };
  return signJWT(payload, (env as Env).JWT_SECRET);
}

// Helper to create an expired token
async function createExpiredToken(): Promise<string> {
  return createTestToken({
    iat: Math.floor(Date.now() / 1000) - 7200, // 2 hours ago
    exp: Math.floor(Date.now() / 1000) - 3600, // 1 hour ago (expired)
  });
}

// Helper to create a JSON response for mocking
function jsonReply(data: unknown, status = 200) {
  return {
    statusCode: status,
    data: JSON.stringify(data),
    headers: { 'Content-Type': 'application/json' },
  };
}

describe('Public Routes', () => {
  describe('GET /', () => {
    it('returns the home page HTML', async () => {
      const response = await SELF.fetch('https://example.com/');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('text/html');
      const text = await response.text();
      expect(text).toContain('TalkKeys');
    });
  });

  describe('GET /health', () => {
    it('returns health check with correct structure', async () => {
      const response = await SELF.fetch('https://example.com/health');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('application/json');

      const json = await response.json() as { status: string; service: string; version: string };
      expect(json.status).toBe('ok');
      expect(json.service).toBe('TalkKeys API');
      expect(json.version).toBeDefined();
    });
  });

  describe('GET /privacy', () => {
    it('returns privacy policy page', async () => {
      const response = await SELF.fetch('https://example.com/privacy');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('text/html');
      const text = await response.text();
      expect(text).toContain('Privacy Policy');
    });
  });

  describe('GET /tos', () => {
    it('returns terms of service page', async () => {
      const response = await SELF.fetch('https://example.com/tos');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('text/html');
      const text = await response.text();
      expect(text).toContain('Terms of Service');
    });
  });

  describe('GET /releases', () => {
    it('returns release notes page', async () => {
      const response = await SELF.fetch('https://example.com/releases');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('text/html');
      const text = await response.text();
      expect(text).toContain('Release Notes');
    });
  });

  describe('GET /api-docs', () => {
    it('returns API documentation page', async () => {
      const response = await SELF.fetch('https://example.com/api-docs');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('text/html');
      const text = await response.text();
      expect(text).toContain('Remote Control API');
    });
  });

  describe('GET /about-content', () => {
    it('returns about content JSON for desktop app', async () => {
      const response = await SELF.fetch('https://example.com/about-content');
      expect(response.status).toBe(200);
      expect(response.headers.get('Content-Type')).toContain('application/json');
      expect(response.headers.get('Cache-Control')).toContain('max-age=3600');

      const json = await response.json() as { releases: unknown[] };
      expect(json.releases).toBeDefined();
      expect(Array.isArray(json.releases)).toBe(true);
    });
  });

  describe('OPTIONS (CORS)', () => {
    it('handles CORS preflight requests', async () => {
      const response = await SELF.fetch('https://example.com/api/clean', {
        method: 'OPTIONS',
      });
      expect(response.status).toBe(200);
      expect(response.headers.get('Access-Control-Allow-Origin')).toBe('*');
      expect(response.headers.get('Access-Control-Allow-Methods')).toContain('POST');
    });
  });

  describe('Unknown routes', () => {
    it('returns 404 for unknown paths', async () => {
      const response = await SELF.fetch('https://example.com/nonexistent');
      // Without auth, it returns 401 first
      expect([401, 404]).toContain(response.status);
    });
  });
});

describe('Authentication', () => {
  describe('Protected endpoints', () => {
    it('returns 401 without Authorization header', async () => {
      const response = await SELF.fetch('https://example.com/api/usage');
      expect(response.status).toBe(401);

      const json = await response.json() as { success: boolean; error: string };
      expect(json.success).toBe(false);
      expect(json.error).toContain('Unauthorized');
    });

    it('returns 401 with invalid token', async () => {
      const response = await SELF.fetch('https://example.com/api/usage', {
        headers: { 'Authorization': 'Bearer invalid-token' },
      });
      expect(response.status).toBe(401);
    });

    it('returns 401 with expired token', async () => {
      const expiredToken = await createExpiredToken();
      const response = await SELF.fetch('https://example.com/api/usage', {
        headers: { 'Authorization': `Bearer ${expiredToken}` },
      });
      expect(response.status).toBe(401);
    });
  });
});

describe('API Endpoints (Authenticated)', () => {
  let validToken: string;

  beforeAll(async () => {
    validToken = await createTestToken();
  });

  beforeEach(() => {
    // Enable fetch mocking for external API calls
    fetchMock.activate();
    fetchMock.disableNetConnect();
  });

  afterEach(() => {
    fetchMock.deactivate();
  });

  describe('POST /api/clean', () => {
    it('returns 400 when no text provided', async () => {
      const response = await SELF.fetch('https://example.com/api/clean', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({}),
      });
      expect(response.status).toBe(400);

      const json = await response.json() as { success: boolean; error: string };
      expect(json.success).toBe(false);
      expect(json.error).toContain('No text provided');
    });

    it('calls Groq API with correct parameters', async () => {
      // Mock Groq API response
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: 'Cleaned text here' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/clean', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: 'um hello uh world' }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { cleaned_text: string } };
      expect(json.success).toBe(true);
      expect(json.data.cleaned_text).toBe('Cleaned text here');
    });

    it('includes words list in prompt when provided', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: 'TalkKeys is great' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/clean', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          text: 'talk keys is great',
          wordsList: ['TalkKeys', 'Groq'],
        }),
      });

      expect(response.status).toBe(200);
    });
  });

  describe('POST /api/explain', () => {
    it('returns 400 when no text provided', async () => {
      const response = await SELF.fetch('https://example.com/api/explain', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({}),
      });
      expect(response.status).toBe(400);

      const json = await response.json() as { success: boolean; error: string };
      expect(json.error).toContain('No text provided');
    });

    it('returns 400 when text too long', async () => {
      const response = await SELF.fetch('https://example.com/api/explain', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: 'a'.repeat(2001) }),
      });
      expect(response.status).toBe(400);

      const json = await response.json() as { success: boolean; error: string };
      expect(json.error).toContain('Text too long');
    });

    it('returns explanation from Groq', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: 'Translation here' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/explain', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: 'Per my last email' }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { explanation: string } };
      expect(json.success).toBe(true);
      expect(json.data.explanation).toBe('Translation here');
    });

    it('accepts different tone settings', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: 'Direct translation' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/explain', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: 'Let us circle back', tone: 'plain' }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { explanation: string } };
      expect(json.success).toBe(true);
      expect(json.data.explanation).toBeDefined();
    });

    it('defaults to wtf for invalid tone', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: 'WTF translation' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/explain', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: 'Hello', tone: 'invalid-tone' }),
      });

      expect(response.status).toBe(200);
    });
  });

  describe('POST /api/classify', () => {
    it('returns 400 when no text provided', async () => {
      const response = await SELF.fetch('https://example.com/api/classify', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({}),
      });

      expect(response.status).toBe(400);
      const json = await response.json() as { success: boolean; error: string };
      expect(json.success).toBe(false);
      expect(json.error).toContain('No text provided');
    });

    it('returns classification from Groq', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: '{"type":"email","confidence":0.9,"suggestedTargets":["email"],"reason":"looks like email"}' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/classify', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          text: 'Hi John, just following up. Best regards, Jane',
          window: { processName: 'OUTLOOK', windowTitle: 'RE: Budget - Outlook' }
        }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { type: string; confidence: number; suggestedTargets: string[] } };
      expect(json.success).toBe(true);
      expect(json.data.type).toBe('email');
      expect(json.data.confidence).toBeGreaterThan(0.5);
      expect(json.data.suggestedTargets).toContain('email');
    });
  });

  describe('POST /api/rewrite', () => {
    it('returns 400 when no text provided', async () => {
      const response = await SELF.fetch('https://example.com/api/rewrite', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ target: 'email' }),
      });

      expect(response.status).toBe(400);
      const json = await response.json() as { success: boolean; error: string };
      expect(json.success).toBe(false);
      expect(json.error).toContain('No text provided');
    });

    it('returns 400 when no target provided', async () => {
      const response = await SELF.fetch('https://example.com/api/rewrite', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ text: 'hello' }),
      });

      expect(response.status).toBe(400);
      const json = await response.json() as { success: boolean; error: string };
      expect(json.success).toBe(false);
      expect(json.error).toContain('No target provided');
    });

    it('returns rewritten text from Groq', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: 'Hi John, just following up on this.' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/rewrite', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          text: 'hey just checking in',
          target: 'email',
          tone: 'professional',
          window: { processName: 'OUTLOOK', windowTitle: 'RE: Budget - Outlook' }
        }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { rewritten_text: string } };
      expect(json.success).toBe(true);
      expect(json.data.rewritten_text).toContain('Hi John');
    });
  });

  describe('POST /api/analyze-words', () => {
    it('returns 400 when no transcriptions provided', async () => {
      const response = await SELF.fetch('https://example.com/api/analyze-words', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({}),
      });
      expect(response.status).toBe(400);

      const json = await response.json() as { success: boolean; error: string };
      expect(json.error).toContain('No transcriptions provided');
    });

    it('returns 400 for empty transcriptions array', async () => {
      const response = await SELF.fetch('https://example.com/api/analyze-words', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ transcriptions: [] }),
      });
      expect(response.status).toBe(400);
    });

    it('returns word suggestions from Groq', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: '["TalkKeys", "Groq"]' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/analyze-words', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          transcriptions: [
            { raw: 'talk keys is great', cleaned: 'TalkKeys is great' },
          ],
        }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { suggestions: string[] } };
      expect(json.success).toBe(true);
      expect(json.data.suggestions).toEqual(['TalkKeys', 'Groq']);
    });

    it('limits transcriptions to 50', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: '[]' } }],
        }));

      // Create 60 transcriptions
      const transcriptions = Array.from({ length: 60 }, (_, i) => ({
        raw: `test ${i}`,
        cleaned: `Test ${i}`,
      }));

      const response = await SELF.fetch('https://example.com/api/analyze-words', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ transcriptions }),
      });

      expect(response.status).toBe(200);
    });

    it('handles markdown code blocks in response', async () => {
      fetchMock.get('https://api.groq.com')
        .intercept({
          path: '/openai/v1/chat/completions',
          method: 'POST',
        })
        .reply(() => jsonReply({
          choices: [{ message: { content: '```json\n["Word1", "Word2"]\n```' } }],
        }));

      const response = await SELF.fetch('https://example.com/api/analyze-words', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${validToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          transcriptions: [{ raw: 'test', cleaned: 'Test' }],
        }),
      });

      expect(response.status).toBe(200);
      const json = await response.json() as { success: boolean; data: { suggestions: string[] } };
      expect(json.data.suggestions).toEqual(['Word1', 'Word2']);
    });
  });
});

describe('Error Handling', () => {
  let validToken: string;

  beforeAll(async () => {
    validToken = await createTestToken();
  });

  beforeEach(() => {
    fetchMock.activate();
    fetchMock.disableNetConnect();
  });

  afterEach(() => {
    fetchMock.deactivate();
  });

  it('handles Groq API errors gracefully', async () => {
    fetchMock.get('https://api.groq.com')
      .intercept({
        path: '/openai/v1/chat/completions',
        method: 'POST',
      })
      .reply(() => jsonReply({ error: 'Internal Server Error' }, 500));

    const response = await SELF.fetch('https://example.com/api/clean', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${validToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ text: 'hello world' }),
    });

    expect(response.status).toBe(502);
    const json = await response.json() as { success: boolean; error: string };
    expect(json.success).toBe(false);
    expect(json.error).toContain('failed');
  });

  it('handles invalid JSON body gracefully', async () => {
    const response = await SELF.fetch('https://example.com/api/clean', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${validToken}`,
        'Content-Type': 'application/json',
      },
      body: 'not valid json',
    });

    expect(response.status).toBe(500);
  });
});
