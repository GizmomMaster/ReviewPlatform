using ReviewPlatform.Domain.Notifications;

namespace ReviewPlatform.Domain.Tests.Notifications;

public sealed class OutboxEmailTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static OutboxEmail NewEmail() => new(EmailType.Invitation, "a@x.ru", "Тема", "<p>Текст</p>", Now);

    [Fact]
    public void New_IsPendingAndDueImmediately()
    {
        var email = NewEmail();

        Assert.Equal((OutboxStatus.Pending, 0, Now), (email.Status, email.Attempts, email.NextAttemptAtUtc));
    }

    [Fact]
    public void MarkSent_StopsRetries()
    {
        var email = NewEmail();

        email.MarkSent(Now.AddSeconds(5));

        Assert.Equal((OutboxStatus.Sent, 1, (DateTime?)null, Now.AddSeconds(5)), (email.Status, email.Attempts, email.NextAttemptAtUtc, email.SentAtUtc));
    }

    [Fact]
    public void MarkAttemptFailed_RetriesWithBackoff_ThenFails()
    {
        var email = NewEmail();

        email.MarkAttemptFailed("smtp down", Now, maxAttempts: 3);
        Assert.Equal((OutboxStatus.Pending, Now.AddMinutes(1)), (email.Status, email.NextAttemptAtUtc));

        email.MarkAttemptFailed("smtp down", Now, maxAttempts: 3);
        Assert.Equal(Now.AddMinutes(2), email.NextAttemptAtUtc);

        email.MarkAttemptFailed("smtp down", Now, maxAttempts: 3);
        Assert.Equal((OutboxStatus.Failed, 3, (DateTime?)null, "smtp down"), (email.Status, email.Attempts, email.NextAttemptAtUtc, email.LastError));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 8)]
    [InlineData(7, 60)]
    [InlineData(20, 60)]
    public void RetryDelay_DoublesUpToAnHour(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), OutboxEmail.RetryDelay(attempts));
}
