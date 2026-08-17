"use client"

import * as React from "react"
import { NumberField } from "@base-ui/react/number-field"
import { Minus, Plus } from "lucide-react"

import { cn } from "@/lib/utils"

/**
 * Sayısal input — native `<input type="number">` spinner'ının tarayıcıya göre
 * değişen, temaya uymayan (açık gri) ok ikonlarının yerini alır. `@base-ui/react`
 * `NumberField` primitive'i üzerine, projenin diğer `components/ui/*` sarmalayıcılarıyla
 * aynı desenle (bkz. `input.tsx`, `button.tsx` renk/odak/disabled dili) kurulur.
 */
function NumberInput({ className, ...props }: NumberField.Root.Props) {
  return (
    <NumberField.Root
      data-slot="number-input"
      className={cn(
        "flex h-8 w-full min-w-0 items-stretch overflow-hidden rounded-md border border-transparent bg-input/50 transition-[color,box-shadow] duration-200 has-[input:focus-visible]:border-ring has-[input:focus-visible]:ring-3 has-[input:focus-visible]:ring-ring/30 data-disabled:pointer-events-none data-disabled:opacity-50",
        className
      )}
      {...props}
    >
      <NumberField.Group className="flex w-full items-stretch">
        <NumberField.Decrement
          data-slot="number-input-decrement"
          className="flex w-7 shrink-0 cursor-pointer items-center justify-center text-muted-foreground transition-colors hover:bg-muted hover:text-foreground data-disabled:pointer-events-none data-disabled:opacity-40"
        >
          <Minus className="size-3.5" aria-hidden="true" />
        </NumberField.Decrement>
        <NumberField.Input
          data-slot="number-input-field"
          className="w-full min-w-0 border-0 bg-transparent px-1 text-center text-base tabular-nums outline-none md:text-sm"
        />
        <NumberField.Increment
          data-slot="number-input-increment"
          className="flex w-7 shrink-0 cursor-pointer items-center justify-center text-muted-foreground transition-colors hover:bg-muted hover:text-foreground data-disabled:pointer-events-none data-disabled:opacity-40"
        >
          <Plus className="size-3.5" aria-hidden="true" />
        </NumberField.Increment>
      </NumberField.Group>
    </NumberField.Root>
  )
}

export { NumberInput }
