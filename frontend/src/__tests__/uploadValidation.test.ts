import { describe, expect, it } from "vitest";
import { validateFile, validateTitle } from "@/lib/uploadValidation";
import { uploadLimits } from "@/constants/upload";
import { contractErrorStrings } from "@/strings/contracts";

describe("validateTitle", () => {
  it("rejects blank titles", () => {
    expect(validateTitle("   ")).toBe(contractErrorStrings.titleRequired);
  });
  it("rejects titles over 200 characters", () => {
    expect(validateTitle("a".repeat(201))).toBe(contractErrorStrings.titleTooLong);
  });
  it("accepts 1 to 200 characters", () => {
    expect(validateTitle("a")).toBeNull();
    expect(validateTitle("a".repeat(200))).toBeNull();
  });
});

describe("validateFile", () => {
  it("requires a file", () => {
    expect(validateFile(null)).toBe(contractErrorStrings.fileRequired);
  });
  it("rejects non-pdf extensions", () => {
    expect(validateFile({ name: "a.docx", size: 10 })).toBe(contractErrorStrings.fileNotPdf);
  });
  it("accepts upper-case extension", () => {
    expect(validateFile({ name: "A.PDF", size: 10 })).toBeNull();
  });
  it("rejects files over 10 MB", () => {
    expect(validateFile({ name: "a.pdf", size: uploadLimits.maxFileBytes + 1 })).toBe(
      contractErrorStrings.fileTooLarge,
    );
  });
});
