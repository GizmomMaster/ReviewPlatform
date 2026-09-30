namespace ReviewPlatform.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Путь к начальной матрице (относительно каталога приложения или абсолютный).</summary>
    public string MatrixFile { get; set; } = "seed/backend-matrix.xlsx";
}
