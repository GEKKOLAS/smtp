export function GradientBackdrop() {
  return (
    <div aria-hidden className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
      <div className="glow-blob -top-32 -left-24 size-[28rem] bg-brand" />
      <div className="glow-blob top-1/3 -right-32 size-[26rem] bg-brand-2" />
      <div className="glow-blob bottom-[-8rem] left-1/3 size-[24rem] bg-warm opacity-25 dark:opacity-30" />
    </div>
  );
}
