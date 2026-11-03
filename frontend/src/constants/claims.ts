export const claimLimits = {
  textMin: 5,
  textMax: 2000,
  coverageMax: 100,
  answerMax: 1000,
  composeBlockSeparator: "\n\n",
  composeLineSeparator: "\n",
  gaugeSize: 96,
  gaugeMax: 100,
} as const;

export const claimHistoryLimits = {
  pageSize: 20,
  firstPage: 1,
  textPreviewMax: 120,
  ellipsis: "…",
  headingId: "claim-history-heading",
} as const;

export const claimFieldIds = {
  heading: "claim-form-heading",
  text: "claim-text",
  textHelp: "claim-text-help",
  coverageHelp: "claim-coverage-help",
  textError: "claim-text-error",
  coverage: "claim-coverage",
  coverageError: "claim-coverage-error",
  formError: "claim-form-error",
  waitNote: "claim-wait-note",
  levelPicker: "claim-level-picker",
  levelPickerLabel: "claim-level-picker-label",
  reevaluateError: "claim-reevaluate-error",
  answer: "claim-follow-up-answer",
  answerError: "claim-follow-up-answer-error",
  answerHelp: "claim-follow-up-answer-help",
} as const;
