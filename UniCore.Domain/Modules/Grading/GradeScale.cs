using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Grading
{
    public sealed record GradeBandSeed(string Letter, decimal MinPercentage, decimal GradePoints, bool IsPassing);

    // A percentage -> letter -> grade points table. Seeded at start (the standard 4.0 scale); read only through the API.
    public sealed class GradeScale : AggregateRoot
    {
        public const int NameMaxLength = 100;
        public const int LetterMaxLength = 5;
        public const decimal MaxGradePoints = 5m;

        // The default scale seeded at start. A percentage of 60 or more passes.
        public static readonly IReadOnlyList<GradeBandSeed> StandardBands =
        [
            new("A+", 97m, 4.0m, true), new("A", 93m, 4.0m, true), new("A-", 90m, 3.7m, true),
            new("B+", 87m, 3.3m, true), new("B", 83m, 3.0m, true), new("B-", 80m, 2.7m, true),
            new("C+", 77m, 2.3m, true), new("C", 73m, 2.0m, true), new("C-", 70m, 1.7m, true),
            new("D+", 67m, 1.3m, true), new("D", 60m, 1.0m, true), new("F", 0m, 0.0m, false)
        ];

        public const string StandardName = "Standard 4.0";

        private readonly List<GradeScaleBand> _bands = [];

        private GradeScale()
        {
        }

        public string Name { get; private set; } = string.Empty;
        public bool IsDefault { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public IReadOnlyList<GradeScaleBand> Bands => _bands.AsReadOnly();

        public static Result<GradeScale> Create(string? name, IReadOnlyCollection<GradeBandSeed> bands, bool isDefault, DateTimeOffset now)
        {
            var clean = name?.Trim();

            if (string.IsNullOrWhiteSpace(clean) || clean.Length > NameMaxLength || bands is null || bands.Count < 2
                || bands.Any(b => string.IsNullOrWhiteSpace(b.Letter) || b.Letter.Trim().Length > LetterMaxLength
                                  || b.MinPercentage < 0 || b.MinPercentage > 100
                                  || b.GradePoints < 0 || b.GradePoints > MaxGradePoints)
                || bands.Select(b => b.Letter.Trim().ToUpperInvariant()).Distinct().Count() != bands.Count
                || bands.Select(b => b.MinPercentage).Distinct().Count() != bands.Count
                || bands.All(b => b.MinPercentage != 0m))
            {
                return GradingErrors.ScaleInvalid;
            }

            var scale = new GradeScale { Id = Guid.NewGuid(), Name = clean, IsDefault = isDefault, CreatedAt = now };

            foreach (var band in bands.OrderByDescending(b => b.MinPercentage))
            {
                scale._bands.Add(GradeScaleBand.Create(scale.Id, band.Letter.Trim(), band.MinPercentage, band.GradePoints, band.IsPassing));
            }

            return scale;
        }

        // The band of a percentage (0-100): the highest band whose minimum it reaches.
        public Result<GradeScaleBand> Resolve(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
            {
                return GradingErrors.PercentageInvalid;
            }

            var band = _bands.OrderByDescending(b => b.MinPercentage).First(b => percentage >= b.MinPercentage);

            return band;
        }
    }
}
