using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Application;

public sealed record GetBomArchiveListRequest(string? FinishedCode);

public sealed record GetBomArchiveListResponse(IReadOnlyList<BomArchiveRecord> Items);

public sealed class GetBomArchiveListUseCase
{
    private readonly IBomArchiveRepository _repo;

    public GetBomArchiveListUseCase(IBomArchiveRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<GetBomArchiveListResponse>> ExecuteAsync(GetBomArchiveListRequest req, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(req.FinishedCode, ct);
        return Result<GetBomArchiveListResponse>.Ok(new GetBomArchiveListResponse(items));
    }
}

