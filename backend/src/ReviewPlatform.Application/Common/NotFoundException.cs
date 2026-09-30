namespace ReviewPlatform.Application.Common;

/// <summary>Сущность не найдена или недоступна текущему пользователю. API превращает в 404.</summary>
public sealed class NotFoundException(string entity, Guid id) : Exception($"{entity} {id} not found.");
