namespace Application.Faqs;

public record FaqDto(Guid Id, string Question, int SortOrder);

public record FaqInput(string Question, int SortOrder);

/// <summary>Admin-curated FAQ suggestions, scoped to an organization.</summary>
public interface IFaqService
{
    Task<IReadOnlyList<FaqDto>> ListAsync(Guid orgId, CancellationToken ct = default);
    Task<FaqDto> CreateAsync(Guid orgId, FaqInput input, CancellationToken ct = default);
    Task<FaqDto> UpdateAsync(Guid orgId, Guid id, FaqInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid orgId, Guid id, CancellationToken ct = default);
}
