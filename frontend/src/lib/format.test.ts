import { describe, expect, it } from "vitest";
import { formatDate } from "@/lib/format";

describe("formatDate", () => {
  it("formats real dates", () => {
    expect(formatDate("2026-10-30T12:00:00Z")).toMatch(/2026/);
  });

  it("does not show year 1 for unset timestamps", () => {
    expect(formatDate("0001-01-01T00:00:00+00:00")).toBe("date unknown");
    expect(formatDate("not a date")).toBe("date unknown");
  });
});
