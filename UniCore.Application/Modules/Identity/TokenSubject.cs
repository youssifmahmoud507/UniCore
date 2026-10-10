namespace UniCore.Application.Modules.Identity
{
    public sealed record TokenSubject(Guid UserId,string UserName,string Email,Guid? PersonId,IReadOnlyCollection<string> Roles);
}
