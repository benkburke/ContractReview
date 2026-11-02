export const errorCodes = {
  fileNotPdf: "contract.file_not_pdf",
  fileTooLarge: "contract.file_too_large",
  titleInvalid: "contract.title_invalid",
  noText: "contract.no_text",
  pdfUnreadable: "contract.pdf_unreadable",
  notFound: "contract.not_found",
  indexingInProgress: "contract.indexing_in_progress",
  embeddingFailed: "contract.embedding_failed",
  claimTextInvalid: "claim.text_invalid",
  claimCoverageLevelInvalid: "claim.coverage_level_invalid",
  claimNotFound: "claim.not_found",
  claimEvaluationFailed: "claim.evaluation_failed",
  notIndexed: "contract.not_indexed",
  embeddingModelMismatch: "contract.embedding_model_mismatch",
} as const;

export const httpStatus = {
  notFound: 404,
  payloadTooLarge: 413,
} as const;
