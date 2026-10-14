namespace UniCore.IntegrationTests.Infrastructure
{
    // One admission request with all its ids, so a test can refer to them later.
    public sealed class AdmitSpec(string? code = null, string? email = null, bool createAccount = false)
    {
        public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

        // A new code per test gives every test its own student number sequence (first number is 0001).
        public static string NewCode() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        public string Code { get; } = code ?? NewCode();

        public string? Email { get; } = email;

        public bool CreateAccount { get; } = createAccount;

        public Guid Person { get; } = Guid.NewGuid();

        public Guid Program { get; } = Guid.NewGuid();

        public Guid Plan { get; } = Guid.NewGuid();

        public Guid AcademicYear { get; } = Guid.NewGuid();

        public DateOnly Admission { get; } = Today.AddDays(-30);

        public string Number(int sequence) => $"{Admission.Year}{Code}{sequence:D4}";

        public object Body => new
        {
            personProfileId = Person,
            email = Email,
            admissionDate = Admission,
            academicProgramId = Program,
            programCode = Code,
            studyPlanVersionId = Plan,
            admissionAcademicYearId = AcademicYear,
            admissionYear = Admission.Year,
            createAccount = CreateAccount
        };

        // Same program and date, a different person.
        public AdmitSpec WithNewPerson() => new(Code, Email, CreateAccount);
    }

}
