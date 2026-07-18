"use client";

import { ApiKeysCard } from "@/components/settings/api-keys-card";
import { ProfileForm } from "@/components/settings/profile-form";
import { PasswordForm } from "@/components/settings/password-form";
import { SessionsCard } from "@/components/settings/sessions-card";
import { PageHeader } from "@/components/app/page-header";
import { useSession } from "@/lib/hooks/use-session";
import { Separator } from "@/components/ui/separator";
import { Settings as SettingsIcon } from "lucide-react";

export default function SettingsPage() {
  const { data: user } = useSession();
  if (!user) return null;

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <PageHeader icon={SettingsIcon} title="Settings" description="Manage your profile and security." />
      <ProfileForm user={user} />
      <Separator />
      <PasswordForm />
      <Separator />
      <SessionsCard />
      <Separator />
      <ApiKeysCard />
    </div>
  );
}
