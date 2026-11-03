using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Application.Features.Claims;
using ContractReview.Application.Features.Claims.Commands.EvaluateClaim;
using ContractReview.Application.Features.Claims.Evaluation;
using ContractReview.Application.Features.Claims.Progress;
using ReviewState = ContractReview.Domain.Claims.ReviewState;
using ContractReview.Application.Features.Claims.Retrieval;
using ContractReview.Application.Options;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractReview.UnitTests.Application;

public class EvaluateClaimProgressTests
{
    private const string Connection = "conn-1";
    private const string Model = "test-embed";

    private sealed class RecordingNotifier : IClaimProgressNotifier
    {
        public List<(string? Connection, ClaimProgressEvent Event)> Pushes { get; } = [];

        public Task NotifyAsync(string? connectionId, ClaimProgressEvent progress)
        {
            Pushes.Add((connectionId, progress));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmbeddings : IEmbeddingClient
    {
        public string ModelName => Model;

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<float[]>>([[1f, 0f]]);
    }

    private sealed class FakeChat(bool fail, ClaimStatus status = ClaimStatus.Covered) : IChatCompletionClient
    {
        public string ModelName => "test-chat";

        public Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken) =>
            fail
                ? throw new ChatFailedException(ChatFailureReasons.Timeout)
                : Task.FromResult(new LlmClaimAnswer(
                    status,
                    90,
                    null,
                    false,
                    [],
                    [],
                    [],
                    "Because.",
                    status == ClaimStatus.InsufficientInformation ? "More?" : null,
                    status == ClaimStatus.InsufficientInformation ? [] : ["C1"]));
    }

    private sealed class FakeReviewer(bool fail = false) : IClaimReviewer
    {
        public int Calls { get; private set; }

        public string ModelName => "test-reviewer";

        public Task<LlmClaimReview> ReviewAsync(ClaimPrompt prompt, ClaimEvaluationResult primary, CancellationToken cancellationToken)
        {
            Calls++;
            return fail
                ? throw new ChatFailedException(ChatFailureReasons.Timeout)
                : Task.FromResult(new LlmClaimReview(true, true, 80, "Agreed.", ["Radiator"], []));
        }
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Guid> SeedIndexedContractAsync(AppDbContext db)
    {
        var contract = Contract.Upload("Title", "c.pdf", "application/pdf", [1], "text", 1, 1000, DateTime.UtcNow);
        contract.StartIndexing();
        contract.ReplaceChunks([new ContractChunkData(0, "Heading", 0, "Radiator covered.", [1f, 0f])], Model, 2, DateTime.UtcNow);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract.Id;
    }

    private static EvaluateClaimHandler NewHandler(
        AppDbContext db,
        RecordingNotifier notifier,
        bool chatFails = false,
        FakeReviewer? reviewer = null,
        ClaimStatus status = ClaimStatus.Covered,
        bool reviewEnabled = true) =>
        new(
            db,
            new FakeEmbeddings(),
            new ClaimRetriever(),
            new FakeChat(chatFails, status),
            reviewer ?? new FakeReviewer(),
            Options.Create(new ClaimsOptions()),
            Options.Create(new ReviewOptions { Enabled = reviewEnabled }),
            notifier,
            TimeProvider.System,
            NullLogger<EvaluateClaimHandler>.Instance);

    [Fact]
    public async Task Successful_evaluation_pushes_retrieving_reasoning_completed_in_order()
    {
        await using var db = NewDb();
        var contractId = await SeedIndexedContractAsync(db);
        var notifier = new RecordingNotifier();

        var dto = await NewHandler(db, notifier).HandleAsync(
            new EvaluateClaimCommand(contractId, "Radiator leaks", null, Connection), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(
            [ClaimProgressStep.Retrieving, ClaimProgressStep.Reasoning, ClaimProgressStep.Reviewing, ClaimProgressStep.Completed],
            notifier.Pushes.Select(p => p.Event.Step));
        Assert.All(notifier.Pushes, p => Assert.Equal(Connection, p.Connection));
        Assert.All(notifier.Pushes, p => Assert.Equal(contractId, p.Event.ContractId));
        Assert.Null(notifier.Pushes[0].Event.ClaimId);
        Assert.Equal(dto.Id, notifier.Pushes[3].Event.ClaimId);
        Assert.Null(notifier.Pushes[3].Event.ErrorCode);
        Assert.Equal(ClaimReviewState.Completed, dto.Review?.State);
        Assert.Equal("test-reviewer", dto.Review?.Model);
    }

    [Fact]
    public async Task Insufficient_information_skips_the_review()
    {
        await using var db = NewDb();
        var contractId = await SeedIndexedContractAsync(db);
        var notifier = new RecordingNotifier();
        var reviewer = new FakeReviewer();

        var dto = await NewHandler(db, notifier, reviewer: reviewer, status: ClaimStatus.InsufficientInformation).HandleAsync(
            new EvaluateClaimCommand(contractId, "Radiator leaks", null, Connection), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Null(dto.Review);
        Assert.Equal(0, reviewer.Calls);
        Assert.DoesNotContain(notifier.Pushes, p => p.Event.Step == ClaimProgressStep.Reviewing);
    }

    [Fact]
    public async Task Disabled_review_is_skipped()
    {
        await using var db = NewDb();
        var contractId = await SeedIndexedContractAsync(db);
        var reviewer = new FakeReviewer();

        var dto = await NewHandler(db, new RecordingNotifier(), reviewer: reviewer, reviewEnabled: false).HandleAsync(
            new EvaluateClaimCommand(contractId, "Radiator leaks", null, Connection), CancellationToken.None);

        Assert.Null(dto?.Review);
        Assert.Equal(0, reviewer.Calls);
    }

    [Fact]
    public async Task Review_failure_still_returns_the_result_with_a_failed_review()
    {
        await using var db = NewDb();
        var contractId = await SeedIndexedContractAsync(db);
        var notifier = new RecordingNotifier();

        var dto = await NewHandler(db, notifier, reviewer: new FakeReviewer(fail: true)).HandleAsync(
            new EvaluateClaimCommand(contractId, "Radiator leaks", null, Connection), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(ClaimState.Evaluated, dto.State);
        Assert.NotNull(dto.Result);
        Assert.Equal(ClaimReviewState.Failed, dto.Review?.State);
        Assert.Equal(ErrorCodes.ReviewFailed, dto.Review?.ErrorCode);
        Assert.Equal(ClaimProgressStep.Completed, notifier.Pushes[^1].Event.Step);
        var stored = await db.Claims.SingleAsync();
        Assert.Equal(ReviewState.Failed, stored.Review?.State);
    }

    [Fact]
    public async Task Chat_failure_pushes_failed_with_claim_id_and_error_code_then_throws()
    {
        await using var db = NewDb();
        var contractId = await SeedIndexedContractAsync(db);
        var notifier = new RecordingNotifier();

        await Assert.ThrowsAsync<ClaimEvaluationFailedException>(() => NewHandler(db, notifier, chatFails: true).HandleAsync(
            new EvaluateClaimCommand(contractId, "Radiator leaks", null, Connection), CancellationToken.None));

        Assert.Equal(
            [ClaimProgressStep.Retrieving, ClaimProgressStep.Reasoning, ClaimProgressStep.Failed],
            notifier.Pushes.Select(p => p.Event.Step));
        var failed = notifier.Pushes[2].Event;
        Assert.NotNull(failed.ClaimId);
        Assert.Equal(ErrorCodes.ClaimEvaluationFailed, failed.ErrorCode);
    }

    [Fact]
    public async Task Validation_failure_pushes_nothing()
    {
        await using var db = NewDb();
        var contractId = await SeedIndexedContractAsync(db);
        var notifier = new RecordingNotifier();

        await Assert.ThrowsAsync<DomainException>(() => NewHandler(db, notifier).HandleAsync(
            new EvaluateClaimCommand(contractId, "   ", null, Connection), CancellationToken.None));

        Assert.Empty(notifier.Pushes);
    }

    [Fact]
    public async Task Unknown_contract_pushes_nothing()
    {
        await using var db = NewDb();
        var notifier = new RecordingNotifier();

        var dto = await NewHandler(db, notifier).HandleAsync(
            new EvaluateClaimCommand(Guid.NewGuid(), "Radiator leaks", null, Connection), CancellationToken.None);

        Assert.Null(dto);
        Assert.Empty(notifier.Pushes);
    }
}
