"use client";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Repeat } from "lucide-react";
import { useState } from "react";

export function EditorKindSwitcher({
  isHtml,
  onConvert,
  converting,
  open: controlledOpen,
  onOpenChange,
}: {
  isHtml: boolean;
  onConvert: () => void;
  converting: boolean;
  /** Pass with onOpenChange to open it from elsewhere (e.g. a menu); no trigger button is rendered then. */
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}) {
  const [innerOpen, setInnerOpen] = useState(false);
  const controlled = controlledOpen !== undefined;
  const open = controlled ? controlledOpen : innerOpen;
  const setOpen = (next: boolean) => (controlled ? onOpenChange?.(next) : setInnerOpen(next));
  const targetLabel = isHtml ? "MJML" : "HTML";

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      {!controlled && (
        <DialogTrigger asChild>
          <Button variant="ghost" size="sm">
            <Repeat className="size-4" />
            Switch to {targetLabel}
          </Button>
        </DialogTrigger>
      )}
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Switch to {targetLabel}?</DialogTitle>
          <DialogDescription>
            {isHtml
              ? "Your HTML is preserved exactly, wrapped so it keeps working — you'll then get the visual builder, MJML source view, and the image placeholders panel."
              : "Your current design is compiled to static HTML — the rendered output stays identical, but the visual builder, MJML source, and image placeholders panel won't be available for this template anymore."}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button
            onClick={() => {
              onConvert();
              setOpen(false);
            }}
            disabled={converting}
          >
            {converting ? "Converting…" : `Switch to ${targetLabel}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
