import { describe, expect, it } from "vitest";
import { formatRate } from "@/lib/api/analytics";

describe("formatRate", () => {
  it("shows a dash when there is nothing to divide by", () => {
    expect(formatRate(null)).toBe("–");
  });

  it("rounds from 10% up and keeps one decimal below", () => {
    expect(formatRate(1)).toBe("100%");
    expect(formatRate(0.456)).toBe("46%");
    expect(formatRate(0.025)).toBe("2.5%");
    expect(formatRate(0.03)).toBe("3%");
    expect(formatRate(0)).toBe("0%");
  });
});
