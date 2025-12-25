import { defineWorkersConfig } from '@cloudflare/vitest-pool-workers/config';

export default defineWorkersConfig({
  test: {
    poolOptions: {
      workers: {
        wrangler: { configPath: './wrangler.toml' },
        miniflare: {
          bindings: {
            GOOGLE_CLIENT_ID: 'test-client-id',
            GOOGLE_CLIENT_SECRET: 'test-client-secret',
            GROQ_API_KEY: 'test-groq-key',
            JWT_SECRET: 'test-jwt-secret-for-testing-only',
            DAILY_LIMIT_SECONDS: '600',
          },
        },
      },
    },
  },
});
