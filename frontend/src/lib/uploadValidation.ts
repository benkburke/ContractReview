import { uploadLimits } from "@/constants/upload";
import { contractErrorStrings } from "@/strings/contracts";

export function validateTitle(title: string): string | null {
  const trimmed = title.trim();
  if (trimmed.length === 0) return contractErrorStrings.titleRequired;
  if (trimmed.length > uploadLimits.titleMaxLength) return contractErrorStrings.titleTooLong;
  return null;
}

export function validateFile(file: { name: string; size: number } | null): string | null {
  if (!file) return contractErrorStrings.fileRequired;
  if (!file.name.toLowerCase().endsWith(uploadLimits.pdfExtension)) {
    return contractErrorStrings.fileNotPdf;
  }
  if (file.size > uploadLimits.maxFileBytes) return contractErrorStrings.fileTooLarge;
  if (file.size === 0) return contractErrorStrings.fileRequired;
  return null;
}
