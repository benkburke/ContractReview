using ContractReview.Domain.Errors;

namespace ContractReview.Domain.Claims;

/// <summary>
/// A natural-language claim against one contract, and its evaluation. A new claim is Evaluating: <see cref="State"/> is
/// null (the API enum has only Evaluated and Failed, so Evaluating exists in memory only and is never stored) until
/// <see cref="Complete"/> or <see cref="Fail"/>, each allowed once.
/// </summary>
public sealed class Claim
{
    private Claim()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The contract this claim is about, by id only.</summary>
    public Guid ContractId { get; private set; }

    public string ClaimText { get; private set; } = string.Empty;

    public string? CoverageLevelRequested { get; private set; }

    /// <summary>Null while Evaluating; Evaluated or Failed once finished (always set in the database).</summary>
    public ClaimState? State { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    // Stored columns of the evaluation result; use Result to read them as one value object.
    public ClaimStatus? Status { get; private set; }

    public int? Confidence { get; private set; }

    public string? CoverageLevel { get; private set; }

    public bool NeedsCoverageLevel { get; private set; }

    /// <summary>Items, options, citations, reasoning and follow-up question as JSON.</summary>
    public string? ResultJson { get; private set; }

    public string? Model { get; private set; }

    public bool IsEvaluating => State is null;

    public ClaimEvaluationResult? Result =>
        Status is { } status && Confidence is { } confidence && ResultJson is not null && Model is not null
            ? ClaimEvaluationResult.FromStored(status, confidence, CoverageLevel, NeedsCoverageLevel, Model, ResultJson)
            : null;

    /// <summary>Starts an Evaluating claim after validating the text (5 to 2000 trimmed characters) and optional level.</summary>
    public static Claim Start(Guid contractId, string? claimText, string? coverageLevel, DateTime createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        ContractId = contractId,
        ClaimText = NormalizeText(claimText),
        CoverageLevelRequested = NormalizeCoverageLevel(coverageLevel),
        CreatedAtUtc = createdAtUtc,
    };

    /// <summary>Trims the claim text and checks it is 5 to 2000 characters.</summary>
    public static string NormalizeText(string? claimText)
    {
        var trimmed = claimText?.Trim();
        if (trimmed is null || trimmed.Length < ClaimRules.MinTextLength || trimmed.Length > ClaimRules.MaxTextLength)
        {
            throw new DomainException(ErrorCodes.ClaimTextInvalid, ErrorMessages.ClaimTextInvalid);
        }

        return trimmed;
    }

    /// <summary>Null stays null; a given level is trimmed and must be 1 to 100 characters.</summary>
    public static string? NormalizeCoverageLevel(string? coverageLevel)
    {
        if (coverageLevel is null)
        {
            return null;
        }

        var trimmed = coverageLevel.Trim();
        if (trimmed.Length < ClaimRules.MinCoverageLevelLength || trimmed.Length > ClaimRules.MaxCoverageLevelLength)
        {
            throw new DomainException(ErrorCodes.ClaimCoverageLevelInvalid, ErrorMessages.ClaimCoverageLevelInvalid);
        }

        return trimmed;
    }

    /// <summary>Stores the result and marks the claim Evaluated. Only allowed while Evaluating.</summary>
    public void Complete(ClaimEvaluationResult result, DateTime completedAtUtc)
    {
        EnsureEvaluating();
        Status = result.Status;
        Confidence = result.Confidence;
        CoverageLevel = result.CoverageLevel;
        NeedsCoverageLevel = result.NeedsCoverageLevel;
        ResultJson = result.ToDetailsJson();
        Model = result.Model;
        State = ClaimState.Evaluated;
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>Marks the claim Failed with an error code and no result (D-033). Only allowed while Evaluating.</summary>
    public void Fail(string errorCode, DateTime completedAtUtc)
    {
        EnsureEvaluating();
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > ClaimRules.MaxErrorCodeLength)
        {
            throw new DomainException(ErrorCodes.DomainRuleViolated, ErrorMessages.DomainRuleViolated);
        }

        ErrorCode = errorCode;
        State = ClaimState.Failed;
        CompletedAtUtc = completedAtUtc;
    }

    private void EnsureEvaluating()
    {
        if (!IsEvaluating)
        {
            throw new DomainException(ErrorCodes.Conflict, ErrorMessages.Conflict, DomainErrorKind.Conflict);
        }
    }
}
