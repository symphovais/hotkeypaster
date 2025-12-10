// Environment bindings
export interface Env {
  DB: D1Database;
  GOOGLE_CLIENT_ID: string;
  GOOGLE_CLIENT_SECRET: string;
  GROQ_API_KEY: string;
  JWT_SECRET: string;
  DAILY_LIMIT_SECONDS: string;
}

// Database types
export interface User {
  id: string;
  google_id: string;
  email: string;
  name: string | null;
  created_at: number;
  last_login: number | null;
}

export interface Usage {
  id: number;
  user_id: string;
  date: string;
  audio_seconds: number;
  requests: number;
}

// JWT payload
export interface JWTPayload {
  sub: string;      // user id
  email: string;
  name: string | null;
  iat: number;      // issued at
  exp: number;      // expiry
}

// Google OAuth types
export interface GoogleTokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  id_token: string;
  scope: string;
  refresh_token?: string;
}

export interface GoogleUserInfo {
  id: string;
  email: string;
  name: string;
  picture: string;
}

// API response types
export interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: string;
}

export interface UsageInfo {
  used_seconds: number;
  limit_seconds: number;
  remaining_seconds: number;
  reset_at: string;
}

export interface AuthTokens {
  access_token: string;
  refresh_token: string;
  expires_in: number;
}
