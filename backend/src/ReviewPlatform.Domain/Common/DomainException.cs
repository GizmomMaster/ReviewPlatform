namespace ReviewPlatform.Domain.Common;

/// <summary>Нарушение бизнес-правила. API превращает его в 400/409.</summary>
public class DomainException(string message) : Exception(message);
