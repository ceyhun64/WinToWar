import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** docs/17-withdrawal-address-suggestions.md Bölüm 3: chip üzerinde kısaltılmış adres gösterimi. */
export function truncateAddress(value: string, front = 6, back = 6): string {
  if (value.length <= front + back + 3) return value
  return `${value.slice(0, front)}...${value.slice(-back)}`
}
