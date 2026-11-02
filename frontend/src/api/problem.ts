import { errorCodes, httpStatus } from "@/constants/errorCodes";
import { claimErrorStrings } from "@/strings/claims";
import { contractErrorStrings, indexStrings } from "@/strings/contracts";

export type ProblemInfo = { status?: number; code?: string; detail?: string };

const codeKey = "code";
const detailKey = "detail";
const statusKey = "status";

/** Shared ProblemDetails reader: `code` is an RFC 7807 extension member, so it is read defensively. */
export function readProblem(body: unknown, status?: number): ProblemInfo {
  const record =
    typeof body === "object" && body !== null
      ? (body as Record<string, unknown>)
      : {};
  const code = record[codeKey];
  const detail = record[detailKey];
  const bodyStatus = record[statusKey];
  return {
    status: status ?? (typeof bodyStatus === "number" ? bodyStatus : undefined),
    code: typeof code === "string" ? code : undefined,
    detail: typeof detail === "string" ? detail : undefined,
  };
}

export type UploadErrorField = "title" | "file" | "form";
export type UploadError = { field: UploadErrorField; message: string };

export function mapUploadProblem(problem: ProblemInfo): UploadError {
  if (problem.status === httpStatus.payloadTooLarge) {
    return { field: "file", message: contractErrorStrings.fileTooLarge };
  }
  switch (problem.code) {
    case errorCodes.fileNotPdf:
      return { field: "file", message: contractErrorStrings.fileNotPdf };
    case errorCodes.fileTooLarge:
      return { field: "file", message: contractErrorStrings.fileTooLarge };
    case errorCodes.noText:
      return { field: "file", message: contractErrorStrings.noText };
    case errorCodes.pdfUnreadable:
      return { field: "file", message: contractErrorStrings.pdfUnreadable };
    case errorCodes.titleInvalid:
      return { field: "title", message: contractErrorStrings.titleRequired };
    default:
      return { field: "form", message: contractErrorStrings.generic };
  }
}

export function mapIndexProblem(problem: ProblemInfo): string {
  switch (problem.code) {
    case errorCodes.indexingInProgress:
      return indexStrings.inProgress;
    case errorCodes.embeddingFailed:
      return indexStrings.embeddingFailed;
    default:
      return contractErrorStrings.generic;
  }
}

export function isNotFound(problem: ProblemInfo): boolean {
  return (
    problem.status === httpStatus.notFound ||
    problem.code === errorCodes.notFound
  );
}

export function mapClaimProblem(problem: ProblemInfo): {
  field: "text" | "coverage" | "form";
  message: string;
} {
  switch (problem.code) {
    case errorCodes.claimTextInvalid:
      return { field: "text", message: claimErrorStrings.textInvalid };
    case errorCodes.claimCoverageLevelInvalid:
      return { field: "coverage", message: claimErrorStrings.coverageInvalid };
    case errorCodes.notIndexed:
      return { field: "form", message: claimErrorStrings.notIndexed };
    case errorCodes.embeddingModelMismatch:
      return { field: "form", message: claimErrorStrings.modelMismatch };
    case errorCodes.claimEvaluationFailed:
      return { field: "form", message: claimErrorStrings.evaluationFailed };
    case errorCodes.notFound:
      return { field: "form", message: claimErrorStrings.contractNotFound };
    default:
      return { field: "form", message: contractErrorStrings.generic };
  }
}

export function isClaimNotFound(problem: ProblemInfo): boolean {
  return (
    problem.status === httpStatus.notFound ||
    problem.code === errorCodes.claimNotFound
  );
}
