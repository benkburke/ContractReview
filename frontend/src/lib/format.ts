import { displayFormat } from "@/constants/upload";

export function formatBytes(value: number | string): string {
  let size = Number(value);
  let unit = 0;
  while (size >= displayFormat.byteStep && unit < displayFormat.byteUnits.length - 1) {
    size /= displayFormat.byteStep;
    unit += 1;
  }
  const rounded = unit === 0 ? String(size) : size.toFixed(1);
  return `${rounded} ${displayFormat.byteUnits[unit]}`;
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(displayFormat.locale);
}
