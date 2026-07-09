"use client";

import { createApiKey, listApiKeys, revokeApiKey } from "@/lib/api/api-keys";
import { formatDate } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

/**
 * API keys for programmatic access (automation / n8n). The secret is shown once
 * on creation and never again.
 */
export function ApiKeysCard() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [freshSecret, setFreshSecret] = useState<string | null>(null);

  const { data: keys } = useQuery({ queryKey: ["api-keys"], queryFn: listApiKeys });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["api-keys"] });

  const create = useMutation({
    mutationFn: () => createApiKey({ name: name.trim() }),
    onSuccess: (created) => {
      setFreshSecret(created.secret);
      setName("");
      invalidate();
    },
    onError: () => toast.error("Could not create the key"),
  });

  const revoke = useMutation({
    mutationFn: revokeApiKey,
    onSuccess: () => { invalidate(); toast.success("Key revoked"); },
    onError: () => toast.error("Could not revoke the key"),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">API keys</CardTitle>
        <CardDescription>
          For automation (e.g. n8n / WhatsApp agents). Send as
          <code className="mx-1 rounded bg-muted px-1">Authorization: Bearer &lt;key&gt;</code>.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {freshSecret && (
          <div className="rounded-md border border-amber-500/40 bg-amber-500/10 p-3">
            <p className="mb-1 text-xs font-medium text-amber-700 dark:text-amber-400">
              Copy this key now — it won&apos;t be shown again.
            </p>
            <div className="flex items-center gap-2">
              <code className="flex-1 truncate rounded bg-background px-2 py-1 text-xs">{freshSecret}</code>
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  navigator.clipboard.writeText(freshSecret);
                  toast.success("Copied");
                }}
              >
                Copy
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setFreshSecret(null)}>Done</Button>
            </div>
          </div>
        )}

        <form
          className="flex gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (name.trim().length > 0) create.mutate();
          }}
        >
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Key name (e.g. n8n automation)"
          />
          <Button type="submit" disabled={create.isPending || name.trim().length === 0}>
            Create key
          </Button>
        </form>

        {keys && keys.length > 0 && (
          <ul className="divide-y">
            {keys.map((key) => (
              <li key={key.id} className="flex items-center justify-between gap-4 py-2">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{key.name}</p>
                  <p className="text-xs text-muted-foreground">
                    {key.prefix}… · created {formatDate(key.createdAt)}
                    {key.lastUsedAt && ` · last used ${formatDate(key.lastUsedAt)}`}
                  </p>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => revoke.mutate(key.id)}
                  disabled={revoke.isPending}
                >
                  Revoke
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
