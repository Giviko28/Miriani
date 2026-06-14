using Application.Faqs;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Faqs;

/// <summary>FAQ CRUD against MS SQL, scoped to one organization and ordered by SortOrder.</summary>
public class FaqService(AppDbContext db) : IFaqService
{
    public async Task<IReadOnlyList<FaqDto>> ListAsync(Guid orgId, CancellationToken ct = default)
    {
        return await db.Faqs
            .Where(f => f.OrgId == orgId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt)
            .Select(f => new FaqDto(f.Id, f.Question, f.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<FaqDto> CreateAsync(Guid orgId, FaqInput input, CancellationToken ct = default)
    {
        Validate(input);
        var faq = new Faq { OrgId = orgId, Question = input.Question.Trim(), SortOrder = input.SortOrder };
        db.Faqs.Add(faq);
        await db.SaveChangesAsync(ct);
        return new FaqDto(faq.Id, faq.Question, faq.SortOrder);
    }

    public async Task<FaqDto> UpdateAsync(Guid orgId, Guid id, FaqInput input, CancellationToken ct = default)
    {
        Validate(input);
        var faq = await db.Faqs.FirstOrDefaultAsync(f => f.Id == id && f.OrgId == orgId, ct)
                  ?? throw new InvalidOperationException("FAQ not found.");
        faq.Question = input.Question.Trim();
        faq.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(ct);
        return new FaqDto(faq.Id, faq.Question, faq.SortOrder);
    }

    public async Task DeleteAsync(Guid orgId, Guid id, CancellationToken ct = default)
    {
        var faq = await db.Faqs.FirstOrDefaultAsync(f => f.Id == id && f.OrgId == orgId, ct);
        if (faq is null) return;
        db.Faqs.Remove(faq);
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(FaqInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Question))
            throw new InvalidOperationException("Question is required.");
        if (input.Question.Length > 500)
            throw new InvalidOperationException("Question must be 500 characters or fewer.");
    }
}
