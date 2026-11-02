export const uploadLimits = {
  maxFileBytes: 10 * 1024 * 1024,
  titleMaxLength: 200,
  pdfExtension: ".pdf",
  accept: "application/pdf,.pdf",
} as const;

export const uploadFormFields = {
  title: "title",
  file: "file",
} as const;

export const uploadFieldIds = {
  title: "upload-title",
  titleError: "upload-title-error",
  file: "upload-file",
  fileError: "upload-file-error",
  formError: "upload-form-error",
} as const;

export const displayFormat = {
  locale: "en-US",
  byteUnits: ["B", "KB", "MB", "GB"],
  byteStep: 1024,
} as const;
