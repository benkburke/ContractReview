import { describe, expect, it } from "vitest";
import { isNotFound, mapUploadProblem, readProblem } from "@/api/problem";
import { errorCodes } from "@/constants/errorCodes";
import { contractErrorStrings } from "@/strings/contracts";

describe("mapUploadProblem", () => {
  it.each([
    [errorCodes.fileNotPdf, "file", contractErrorStrings.fileNotPdf],
    [errorCodes.fileTooLarge, "file", contractErrorStrings.fileTooLarge],
    [errorCodes.noText, "file", contractErrorStrings.noText],
    [errorCodes.pdfUnreadable, "file", contractErrorStrings.pdfUnreadable],
    [errorCodes.titleInvalid, "title", contractErrorStrings.titleRequired],
  ])("maps %s to the %s field", (code, field, message) => {
    expect(mapUploadProblem(readProblem({ code }, 400))).toEqual({
      field,
      message,
    });
  });

  it("maps 413 to the too-large message on the file field", () => {
    expect(mapUploadProblem(readProblem({}, 413))).toEqual({
      field: "file",
      message: contractErrorStrings.fileTooLarge,
    });
  });

  it("falls back to a form-level error", () => {
    expect(mapUploadProblem(readProblem({ code: "x" }, 500)).field).toBe(
      "form",
    );
  });
});

describe("isNotFound", () => {
  it("detects 404 and the not_found code", () => {
    expect(isNotFound(readProblem(undefined, 404))).toBe(true);
    expect(isNotFound(readProblem({ code: errorCodes.notFound }))).toBe(true);
    expect(isNotFound(readProblem({}, 500))).toBe(false);
  });
});
