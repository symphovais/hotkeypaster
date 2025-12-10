import { Env, User, Usage } from './types';

export async function getUserByGoogleId(env: Env, googleId: string): Promise<User | null> {
  const result = await env.DB.prepare(
    'SELECT * FROM users WHERE google_id = ?'
  ).bind(googleId).first<User>();
  return result;
}

export async function getUserById(env: Env, userId: string): Promise<User | null> {
  const result = await env.DB.prepare(
    'SELECT * FROM users WHERE id = ?'
  ).bind(userId).first<User>();
  return result;
}

export async function createUser(
  env: Env,
  googleId: string,
  email: string,
  name: string | null
): Promise<User> {
  const id = crypto.randomUUID();
  const now = Math.floor(Date.now() / 1000);

  await env.DB.prepare(
    'INSERT INTO users (id, google_id, email, name, created_at, last_login) VALUES (?, ?, ?, ?, ?, ?)'
  ).bind(id, googleId, email, name, now, now).run();

  return {
    id,
    google_id: googleId,
    email,
    name,
    created_at: now,
    last_login: now
  };
}

export async function updateLastLogin(env: Env, userId: string): Promise<void> {
  const now = Math.floor(Date.now() / 1000);
  await env.DB.prepare(
    'UPDATE users SET last_login = ? WHERE id = ?'
  ).bind(now, userId).run();
}

export async function getTodayUsage(env: Env, userId: string): Promise<Usage | null> {
  const today = new Date().toISOString().split('T')[0]; // YYYY-MM-DD

  const result = await env.DB.prepare(
    'SELECT * FROM usage WHERE user_id = ? AND date = ?'
  ).bind(userId, today).first<Usage>();

  return result;
}

export async function incrementUsage(
  env: Env,
  userId: string,
  audioSeconds: number
): Promise<Usage> {
  const today = new Date().toISOString().split('T')[0];

  // Upsert usage record
  await env.DB.prepare(`
    INSERT INTO usage (user_id, date, audio_seconds, requests)
    VALUES (?, ?, ?, 1)
    ON CONFLICT(user_id, date) DO UPDATE SET
      audio_seconds = audio_seconds + excluded.audio_seconds,
      requests = requests + 1
  `).bind(userId, today, audioSeconds).run();

  // Return updated record
  const result = await env.DB.prepare(
    'SELECT * FROM usage WHERE user_id = ? AND date = ?'
  ).bind(userId, today).first<Usage>();

  return result!;
}

export async function getUsageStats(env: Env, userId: string): Promise<{
  used: number;
  limit: number;
  remaining: number;
}> {
  const limit = parseInt(env.DAILY_LIMIT_SECONDS) || 600;
  const usage = await getTodayUsage(env, userId);
  const used = usage?.audio_seconds ?? 0;

  return {
    used,
    limit,
    remaining: Math.max(0, limit - used)
  };
}
