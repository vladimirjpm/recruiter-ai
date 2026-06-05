/**
 * Converts raw candidate name from parser into a display-friendly format.
 *
 * Handles:
 *   "jane_smith"              → "Jane Smith"
 *   "CV - Vladimir Barentsev" → "Vladimir Barentsev"
 *   "CV - Vladimir Barentsev.pdf" → "Vladimir Barentsev"
 *   "JOHN DOE"                → "John Doe"
 */
export function formatCandidateName(raw: string): string {
  return raw
    .replace(/\.pdf$/i, '')           // strip trailing .pdf
    .replace(/^CV\s*[-–]\s*/i, '')    // strip "CV - " or "CV – " prefix
    .replace(/_/g, ' ')               // underscores → spaces
    .trim()
    .replace(/\b\w/g, c => c.toUpperCase()); // title-case each word
}
