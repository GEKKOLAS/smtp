import type { NextConfig } from "next";

// Backend origin; the browser only ever talks to the Next.js origin so session
// cookies stay first-party (spec: docs/spec/03-architecture.md §1).
const apiUrl = process.env.API_URL ?? "http://localhost:5001";

const nextConfig: NextConfig = {
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
