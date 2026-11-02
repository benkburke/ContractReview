namespace ContractReview.Domain.Claims;

/// <summary>Limits for claims and evaluation results (spec "Validation rules and limits").</summary>
public static class ClaimRules
{
    public const int MinTextLength = 5;
    public const int MaxTextLength = 2000;
    public const int MinCoverageLevelLength = 1;
    public const int MaxCoverageLevelLength = 100;

    public const int MinConfidence = 0;
    public const int MaxConfidence = 100;
    public const int MaxCoveredItems = 20;
    public const int MaxExcludedItems = 20;
    public const int MaxCitations = 10;
    public const int MaxCoverageLevelOptions = 10;
    public const int MaxItemLength = 300;
    public const int MaxReasoningLength = 4000;
    public const int MaxFollowUpQuestionLength = 500;
    public const int MaxExcerptLength = 500;

    public const int MaxStateLength = 20;
    public const int MaxStatusLength = 30;
    public const int MaxErrorCodeLength = 100;
    public const int MaxModelLength = 200;
}
