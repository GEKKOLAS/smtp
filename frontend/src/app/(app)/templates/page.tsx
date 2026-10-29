"use client";

import { archiveTemplate, deleteTemplate, duplicateTemplate, listTemplates } from "@/lib/api/templates";
import { formatDate } from "@/lib/format";
import { queryKeys } from "@/lib/query/query-keys";
import { EmptyState } from "@/components/app/empty-state";
import { PageHeader } from "@/components/app/page-header";
import { NewTemplateDialog } from "@/components/templates/new-template-dialog";
import { TemplatePreviewThumb } from "@/components/templates/template-preview-thumb";
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
import { LayoutTemplate, MoreVertical, Send } from "lucide-react";
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
      <PageHeader
        icon={LayoutTemplate}
        eyebrow={data ? `${data.totalCount} ${archived ? "archived" : "active"}` : undefined}
        title="Templates"
        description="Reusable, responsive email designs."
        action={<NewTemplateDialog />}
      />

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
        <EmptyState
          icon={LayoutTemplate}
          title={search ? "No templates match your search" : archived ? "Nothing archived" : "No templates yet"}
          description={
            !search && !archived ? "Design one visually, write MJML, or let AI draft it from a prompt." : undefined
          }
          action={!search && !archived ? <NewTemplateDialog /> : undefined}
        />
      )}

      {data && data.items.length > 0 && (
        <ul className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {data.items.map((template) => (
            <li key={template.id} className="odd:-rotate-[0.6deg] even:rotate-[0.5deg] dark:odd:rotate-0 dark:even:rotate-0">
              <Card className="flex h-full flex-col overflow-hidden py-0">
                <Link
                  href={`/templates/${template.id}/edit`}
                  className="block border-b-2 transition-opacity hover:opacity-90 dark:border-b"
                >
                  <TemplatePreviewThumb versionId={template.currentVersionId} />
                </Link>
                <CardContent className="flex flex-1 flex-col gap-3 py-3">
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <Link
                        href={`/templates/${template.id}/edit`}
                        className="truncate font-bold hover:underline dark:font-semibold"
                      >
                        {template.name}
                      </Link>
                      {template.currentVersionNumber && (
                        <Badge variant="secondary">v{template.currentVersionNumber}</Badge>
                      )}
                    </div>
                    <p className="mt-0.5 truncate text-xs text-muted-foreground">
                      {template.description || "No description"} · updated {formatDate(template.updatedAt)}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button asChild variant="outline" size="sm" className="flex-1">
                      <Link href={`/templates/${template.id}/edit`}>Edit</Link>
                    </Button>
                    <Button asChild variant="gradient" size="sm" className="flex-1">
                      <Link href={`/compose?templateId=${template.id}`}>
                        <Send className="size-4" />
                        Send
                      </Link>
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
