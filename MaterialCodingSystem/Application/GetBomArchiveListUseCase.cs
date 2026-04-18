using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

public sealed record GetBomArchiveListRequest(string? FinishedCode);

public sealed record GetBomArchiveListResponse(IReadOnlyList<BomArchiveRecord> Items);

public sealed class GetBomArchiveListUseCase
{
    private readonly IBomArchiveRepository _repo;
    private readonly ILogger<GetBomArchiveListUseCase> _logger;

    public GetBomArchiveListUseCase(IBomArchiveRepository repo, ILogger<GetBomArchiveListUseCase>? logger = null)
    {
        _repo = repo;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GetBomArchiveListUseCase>.Instance;
    }

    public Task<Result<GetBomArchiveListResponse>> ExecuteAsync(GetBomArchiveListRequest req, CancellationToken ct = default)
        => McsLoggingExtensions.RunUseCaseAsync(_logger, McsActions.BomListArchive, req.FinishedCode ?? "*", ct,
            async () =>
            {
                var items = await _repo.ListAsync(req.FinishedCode, ct);
                return Result<GetBomArchiveListResponse>.Ok(new GetBomArchiveListResponse(items));
            },
            static r => r.IsSuccess && r.Data is not null ? ("entry_count", r.Data.Items.Count) : null);
}

