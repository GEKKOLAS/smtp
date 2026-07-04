export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-full flex-1 flex-col items-center justify-center bg-muted/40 px-4 py-12">
      <div className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <h1 className="text-2xl font-semibold tracking-tight">Mail Template Hub</h1>
          <p className="text-sm text-muted-foreground">
            Design once, send from your own Gmail and Outlook.
          </p>
        </div>
        {children}
      </div>
    </div>
  );
}
