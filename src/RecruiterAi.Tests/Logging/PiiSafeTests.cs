using RecruiterAi.Infrastructure.Logging;

namespace RecruiterAi.Tests.Logging;

public class PiiSafeTests
{
    // ── MaskEmail ─────────────────────────────────────────────────────────────

    [Fact]
    public void MaskEmail_TypicalAddress_MasksLocalPart()
    {
        var result = PiiSafe.MaskEmail("john.smith@gmail.com");

        Assert.Equal("j***@gmail.com", result);
    }

    [Fact]
    public void MaskEmail_DoesNotReturnOriginalEmail()
    {
        const string email = "john.smith@gmail.com";
        var result = PiiSafe.MaskEmail(email);

        Assert.NotEqual(email, result);
    }

    [Fact]
    public void MaskEmail_Null_ReturnsNull()
    {
        Assert.Null(PiiSafe.MaskEmail(null));
    }

    [Fact]
    public void MaskEmail_Empty_ReturnsNull()
    {
        Assert.Null(PiiSafe.MaskEmail(""));
    }

    [Fact]
    public void MaskEmail_NoAtSign_ReturnsStars()
    {
        Assert.Equal("***", PiiSafe.MaskEmail("notanemail"));
    }

    [Fact]
    public void MaskEmail_SingleCharLocalPart_MasksCorrectly()
    {
        var result = PiiSafe.MaskEmail("a@example.com");

        Assert.Equal("a***@example.com", result);
    }

    // ── Fingerprint ───────────────────────────────────────────────────────────

    [Fact]
    public void Fingerprint_DoesNotReturnOriginalText()
    {
        const string text = "This is some CV content that must never appear in logs.";
        var result = PiiSafe.Fingerprint(text);

        Assert.DoesNotContain(text, result);
    }

    [Fact]
    public void Fingerprint_ContainsCorrectLength()
    {
        const string text = "Hello, world!";
        var result = PiiSafe.Fingerprint(text);

        Assert.Contains($"len={text.Length}", result);
    }

    [Fact]
    public void Fingerprint_SameInput_ProducesSameOutput()
    {
        const string text = "deterministic input";

        Assert.Equal(PiiSafe.Fingerprint(text), PiiSafe.Fingerprint(text));
    }

    [Fact]
    public void Fingerprint_DifferentInputs_ProduceDifferentOutputs()
    {
        Assert.NotEqual(
            PiiSafe.Fingerprint("candidate A resume text"),
            PiiSafe.Fingerprint("candidate B resume text"));
    }

    [Fact]
    public void Fingerprint_Null_ReturnsSafeDefault()
    {
        Assert.Equal("len=0,h=empty", PiiSafe.Fingerprint(null));
    }

    [Fact]
    public void Fingerprint_Empty_ReturnsSafeDefault()
    {
        Assert.Equal("len=0,h=empty", PiiSafe.Fingerprint(""));
    }

    // ── MaskPhone ─────────────────────────────────────────────────────────────

    [Fact]
    public void MaskPhone_DoesNotReturnFullNumber()
    {
        const string phone = "+7 (999) 123-45-67";
        var result = PiiSafe.MaskPhone(phone);

        Assert.NotEqual(phone, result);
        Assert.DoesNotContain("999", result!);
    }

    [Fact]
    public void MaskPhone_KeepsLastFourDigits()
    {
        var result = PiiSafe.MaskPhone("+7 (999) 123-45-67");

        Assert.Equal("***4567", result);
    }

    [Fact]
    public void MaskPhone_Null_ReturnsNull()
    {
        Assert.Null(PiiSafe.MaskPhone(null));
    }

    [Fact]
    public void MaskPhone_Empty_ReturnsNull()
    {
        Assert.Null(PiiSafe.MaskPhone(""));
    }

    [Fact]
    public void MaskPhone_ShortNumber_KeepsUpToFourDigits()
    {
        // 3 digits only — keeps all of them
        var result = PiiSafe.MaskPhone("123");

        Assert.Equal("***123", result);
    }
}
