// Feature flags — controlled via environment variables.
// All VITE_* vars are baked in at build time (no runtime fetch needed).
//
// Set in .env.development or .env.production, or in the hosting platform's
// environment variables panel (Vercel / Railway).

export const features = {
  // Synthetic CV Generator: generates fake CVs for testing screening quality.
  // Not part of the recruiter workflow — dev/QA tooling only.
  // Disable in production: leave VITE_ENABLE_SYNTHETIC_GENERATOR unset or set to "false".
  syntheticCvGenerator: import.meta.env.VITE_ENABLE_SYNTHETIC_GENERATOR === 'true',
};
