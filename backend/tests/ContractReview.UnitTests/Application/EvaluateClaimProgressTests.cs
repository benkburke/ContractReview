using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Application.Features.Claims;
using ContractReview.Application.Features.Claims.Commands.EvaluateClaim;
using ContractReview.Application.Features.Claims.Progress;
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

    private sealed class FakeChat(bool fail) : IChatCompletionClient
    {
        public string ModelName => "test-chat";

        public Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken) =>
            fail
                ? throw new ChatFailedException(ChatFailureReasons.Timeout)
                : Task.FromResult(new LlmClaimAnswer(ClaimStatus.Covered, 90, null, false, [], [], [], "Because.", null, ["C1"]));
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

    private static EvaluateClaimHandler NewHandler(AppDbContext db, RecordingNotifier notifier, bool chatFails = false) =>
        new(
            db,
            new FakeEmbeddings(),
            new ClaimRetriever(),
            new FakeChat(chatFails),
            Options.Create(new ClaimsOptions()),
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
            [ClaimProgressStep.Retrieving, ClaimProgressStep.Reasoning, ClaimProgressStep.Completed],
            notifier.Pushes.Select(p => p.Event.Step));
        Assert.All(notifier.Pushes, p => Assert.Equal(Connection, p.Connection));
        Assert.All(notifier.Pushes, p => Assert.Equal(contractId, p.Event.ContractId));
        Assert.Null(notifier.Pushes[0].Event.ClaimId);
        Assert.Equal(dto.Id, notifier.Pushes[2].Event.ClaimId);
        Assert.Null(notifier.Pushes[2].Event.ErrorCode);
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
