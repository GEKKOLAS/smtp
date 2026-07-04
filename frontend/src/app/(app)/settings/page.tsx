"use client";

import { ProfileForm } from "@/components/settings/profile-form";
import { PasswordForm } from "@/components/settings/password-form";
import { SessionsCard } from "@/components/settings/sessions-card";
import { useSession } from "@/lib/hooks/use-session";
import { Separator } from "@/components/ui/separator";

export default function SettingsPage() {
  const { data: user } = useSession();
  if (!user) return null;

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
        <p className="text-muted-foreground">Manage your profile and security.</p>
      </header>
      <ProfileForm user={user} />
      <Separator />
      <PasswordForm />
      <Separator />
      <SessionsCard />
    </div>
  );
}
