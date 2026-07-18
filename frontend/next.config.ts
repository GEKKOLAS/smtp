import type { NextConfig } from "next";

// Backend origin; the browser only ever talks to the Next.js origin so session
// cookies stay first-party (spec: docs/spec/03-architecture.md §1).
const apiUrl = process.env.API_URL ?? "http://localhost:5001";

const nextConfig: NextConfig = {
  // Next.js's rewrite proxy defaults to a 30s timeout (proxy-request.js:
  // `proxyTimeout || 30000`) — AI template generation can legitimately run
  // longer than that (retries on a flaky connection to Anthropic), so this
  // must be raised or every long-running rewritten request gets killed at
  // exactly 30s regardless of how the backend itself is doing.
  experimental: {
    proxyTimeout: 180_000,
  },
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${apiUrl}/api/:path*`,
      },
      {
        source: "/healthz",
        destination: `${apiUrl}/healthz`,
      },
    ];
  },
};

export default nextConfig;
