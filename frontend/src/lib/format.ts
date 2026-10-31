export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ["KB", "MB", "GB"];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unit]}`;
}

export function formatDate(iso: string): string {
  const date = new Date(iso);
  // Template versions saved before the timestamp fix carry 0001-01-01.
  if (Number.isNaN(date.getTime()) || date.getUTCFullYear() < 1970) return "date unknown";
  return date.toLocaleDateString(undefined, { dateStyle: "medium" });
}

/** Time of day for today's dates, otherwise a short "Oct 28" — the usual mail-list format. */
export function formatMailTime(iso: string | null): string {
  if (!iso) return "";
  const date = new Date(iso);
  return date.toDateString() === new Date().toDateString()
    ? date.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })
    : date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}
