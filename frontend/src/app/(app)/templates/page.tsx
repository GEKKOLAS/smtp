"use client";

import { archiveTemplate, deleteTemplate, duplicateTemplate, listTemplates } from "@/lib/api/templates";
import { formatDate } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { NewTemplateDialog } from "@/components/templates/new-template-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MoreVertical } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState("active");
  const archived = tab === "archived";

  const { data, isLoading, isError } = useQuery({
    queryKey: queryKeys.templates({ search, archived }),
    queryFn: () => listTemplates({ search: search || undefined, archived }),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["templates"] });

  const duplicate = useMutation({
    mutationFn: duplicateTemplate,
    onSuccess: () => { refresh(); toast.success("Template duplicated"); },
    onError: () => toast.error("Could not duplicate"),
  });
  const setArchived = useMutation({
    mutationFn: ({ id, value }: { id: string; value: boolean }) => archiveTemplate(id, value),
    onSuccess: () => { refresh(); toast.success("Updated"); },
  });
  const remove = useMutation({
    mutationFn: deleteTemplate,
    onSuccess: () => { refresh(); toast.success("Template deleted"); },
    onError: () => toast.error("Could not delete"),
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <header className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Templates</h1>
          <p className="text-muted-foreground">Reusable, responsive email designs.</p>
        </div>
        <NewTemplateDialog />
      </header>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="active">Active</TabsTrigger>
            <TabsTrigger value="archived">Archived</TabsTrigger>
          </TabsList>
        </Tabs>
        <Input
          placeholder="Search templates…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full sm:w-64"
        />
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading templates…</p>}
      {isError && <p className="text-sm text-destructive">Could not load templates.</p>}

      {data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed py-16 text-center">
          <p className="mb-4 text-sm text-muted-foreground">
            {search ? "No templates match your search." : "No templates yet."}
          </p>
          {!search && !archived && <NewTemplateDialog />}
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="space-y-2">
          {data.items.map((template) => (
            <li key={template.id}>
              <Card>
                <CardContent className="flex items-center justify-between gap-4 py-3">
                  <Link href={`/templates/${template.id}/edit`} className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span className="truncate font-medium">{template.name}</span>
                      {template.currentVersionNumber && (
                        <Badge variant="secondary">v{template.currentVersionNumber}</Badge>
                      )}
                    </div>
                    <p className="truncate text-xs text-muted-foreground">
                      {template.description || "No description"} · updated {formatDate(template.updatedAt)}
                    </p>
                  </Link>
                  <div className="flex shrink-0 items-center gap-2">
                    <Button asChild variant="outline" size="sm">
                      <Link href={`/templates/${template.id}/edit`}>Edit</Link>
                    </Button>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" aria-label="Template actions">
                          <MoreVertical className="size-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onSelect={() => duplicate.mutate(template.id)}>
                          Duplicate
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onSelect={() => setArchived.mutate({ id: template.id, value: !archived })}
                        >
                          {archived ? "Unarchive" : "Archive"}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          variant="destructive"
                          onSelect={() => {
                            if (confirm(`Delete "${template.name}"? Send history is preserved.`)) {
                              remove.mutate(template.id);
                            }
                          }}
                        >
                          Delete
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
