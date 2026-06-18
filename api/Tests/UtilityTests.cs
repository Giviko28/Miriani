using Application.Faqs;
using Infrastructure.Auth;
using Infrastructure.Faqs;
using Infrastructure.Users;
using Xunit;

namespace Tests;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generates_requested_length_with_all_character_classes()
    {
        var pw = PasswordGenerator.Generate(14);

        Assert.Equal(14, pw.Length);
        Assert.Contains(pw, char.IsLower);
        Assert.Contains(pw, char.IsUpper);
        Assert.Contains(pw, char.IsDigit);
        Assert.Contains(pw, c => "!@#$%*?".Contains(c));
    }

    [Fact]
    public void Successive_passwords_differ()
    {
        Assert.NotEqual(PasswordGenerator.Generate(), PasswordGenerator.Generate());
    }
}

public class RefreshTokensTests
{
    [Fact]
    public void Hash_is_deterministic_for_the_same_input()
    {
        Assert.Equal(RefreshTokens.Hash("abc"), RefreshTokens.Hash("abc"));
    }

    [Fact]
    public void Raw_tokens_are_unique()
    {
        Assert.NotEqual(RefreshTokens.NewRawToken(), RefreshTokens.NewRawToken());
    }
}

public class FaqServiceTests
{
    [Fact]
    public async Task Create_rejects_empty_question()
    {
        using var db = TestSupport.NewDb();
        var svc = new FaqService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAsync(Guid.NewGuid(), new FaqInput("   ", 1)));
    }

    [Fact]
    public async Task Create_then_list_returns_ordered_faqs()
    {
        using var db = TestSupport.NewDb();
        var orgId = Guid.NewGuid();
        var svc = new FaqService(db);
        await svc.CreateAsync(orgId, new FaqInput("Second", 2));
        await svc.CreateAsync(orgId, new FaqInput("First", 1));

        var list = await svc.ListAsync(orgId);

        Assert.Equal(2, list.Count);
        Assert.Equal("First", list[0].Question);
    }
}
