"use client";

import type { DailyPoint } from "@/lib/api/analytics";
import { CheckCircle2, CornerDownLeft, XCircle, type LucideIcon } from "lucide-react";
import { useCallback, useState } from "react";

// Status colors are fixed across themes (they mean a state, not a series) and
// always travel with an icon and a label, never color alone.
const SERIES: { key: keyof Omit<DailyPoint, "date">; label: string; color: string; icon: LucideIcon }[] = [
  { key: "accepted", label: "Accepted", color: "#0ca30c", icon: CheckCircle2 },
  { key: "bounced", label: "Bounced", color: "#ec835a", icon: CornerDownLeft },
  { key: "failed", label: "Failed", color: "#d03b3b", icon: XCircle },
];

const HEIGHT = 220;
const MARGIN = { top: 12, right: 8, bottom: 26, left: 34 };
const GAP = 2;
const RADIUS = 4;

const dayLabel = (iso: string) =>
  new Date(`${iso}T00:00:00Z`).toLocaleDateString(undefined, { month: "short", day: "numeric", timeZone: "UTC" });

/** A 0-based axis top and step that land on round numbers, with about four ticks. */
function niceScale(max: number): { top: number; step: number } {
  if (max <= 4) return { top: 4, step: 1 };
  const rough = max / 4;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 5, 10].map((m) => m * magnitude).find((s) => s >= rough) ?? 10 * magnitude;
  return { top: Math.ceil(max / step) * step, step };
}

/** A column segment; the top one gets rounded data-end corners, the baseline stays square. */
function segmentPath(x: number, y: number, w: number, h: number, rounded: boolean): string {
  const r = rounded ? Math.min(RADIUS, h, w / 2) : 0;
  return `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${y + h}Z`;
}

export function DailyLegend() {
  return (
    <ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground" aria-label="Legend">
      {SERIES.map((s) => (
        <li key={s.key} className="flex items-center gap-1.5">
          <s.icon className="size-3.5" style={{ color: s.color }} aria-hidden />
          {s.label}
        </li>
      ))}
    </ul>
  );
}

export function DailyChart({ points }: { points: DailyPoint[] }) {
  const [width, setWidth] = useState(0);
  const [active, setActive] = useState<number | null>(null);

  const measure = useCallback((node: HTMLDivElement | null) => {
    if (!node) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  const totals = points.map((p) => p.accepted + p.bounced + p.failed);
  const { top, step } = niceScale(Math.max(0, ...totals));
  const plotW = Math.max(0, width - MARGIN.left - MARGIN.right);
  const plotH = HEIGHT - MARGIN.top - MARGIN.bottom;
  const band = points.length > 0 ? plotW / points.length : 0;
  const barW = Math.max(2, Math.min(24, band * 0.62));
  const y = (value: number) => MARGIN.top + plotH - (value / top) * plotH;
  const ticks = Array.from({ length: Math.round(top / step) + 1 }, (_, i) => i * step);
  const labelIndexes = [...new Set([0, Math.floor((points.length - 1) / 2), points.length - 1])];
  const hovered = active !== null ? points[active] : null;

  return (
    <div ref={measure} className="relative" onMouseLeave={() => setActive(null)}>
      {width > 0 && (
        <svg width={width} height={HEIGHT} role="img" aria-label="Recipients per day, by outcome">
          {ticks.map((tick) => (
            <g key={tick}>
              <line
                x1={MARGIN.left}
                x2={width - MARGIN.right}
                y1={y(tick)}
                y2={y(tick)}
                className={tick === 0 ? "stroke-foreground/30" : "stroke-foreground/10"}
                strokeWidth={1}
              />
              <text
                x={MARGIN.left - 8}
                y={y(tick)}
                textAnchor="end"
                dominantBaseline="middle"
                className="fill-muted-foreground font-mono text-[10px]"
              >
                {tick}
              </text>
            </g>
          ))}

          {points.map((point, i) => {
            const x = MARGIN.left + i * band + (band - barW) / 2;
            const visible = SERIES.filter((s) => point[s.key] > 0);
            let base = point.accepted + point.bounced + point.failed;
            return (
              <g key={point.date}>
                {active === i && (
                  <rect
                    x={MARGIN.left + i * band}
                    y={MARGIN.top}
                    width={band}
                    height={plotH}
                    className="fill-foreground/5"
                  />
                )}
                {/* Drawn top-down so each segment's gap sits above the one below it. */}
                {[...visible].reverse().map((series, j) => {
                  const value = point[series.key];
                  const segTop = y(base);
                  base -= value;
                  const h = y(base) - segTop - (j < visible.length - 1 ? GAP : 0);
                  if (h <= 0) return null;
                  return (
                    <path
                      key={series.key}
                      d={segmentPath(x, segTop, barW, h, j === 0)}
                      fill={series.color}
                    />
                  );
                })}
                <rect
                  x={MARGIN.left + i * band}
                  y={MARGIN.top}
                  width={band}
                  height={plotH + MARGIN.bottom}
                  fill="transparent"
                  onMouseEnter={() => setActive(i)}
                />
              </g>
            );
          })}

          {labelIndexes.map((i) => (
            <text
              key={i}
              x={MARGIN.left + i * band + band / 2}
              y={HEIGHT - 8}
              textAnchor={i === 0 ? "start" : i === points.length - 1 ? "end" : "middle"}
              className="fill-muted-foreground font-mono text-[10px]"
            >
              {points[i] && dayLabel(points[i].date)}
            </text>
          ))}
        </svg>
      )}

      {hovered && active !== null && (
        <div
          role="tooltip"
          className="pointer-events-none absolute top-1 z-10 w-40 rounded-lg border-2 bg-popover px-3 py-2 text-xs shadow-hard-sm dark:border dark:shadow-none"
          style={{
            left: Math.min(Math.max(0, MARGIN.left + active * band + band / 2 - 80), Math.max(0, width - 160)),
          }}
        >
          <p className="mb-1 font-semibold">{dayLabel(hovered.date)}</p>
          {SERIES.map((s) => (
            <p key={s.key} className="flex items-center gap-1.5">
              <s.icon className="size-3" style={{ color: s.color }} aria-hidden />
              <span className="flex-1 text-muted-foreground">{s.label}</span>
              <span className="font-mono font-semibold">{hovered[s.key]}</span>
            </p>
          ))}
        </div>
      )}
    </div>
  );
}
