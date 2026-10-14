namespace UniCore.Application.Modules.Finance
{
    public sealed record ChargeGenerationDto(int StudentsProcessed, int StudentsWithoutStructure, int ChargesCreated, int ChargesAlreadyExisting);

}
