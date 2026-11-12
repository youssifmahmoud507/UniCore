using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;

namespace UniCore.UnitTests.Modules.Reporting
{
    // The real handlers with the real PermissionAuthorizer on in-memory fakes.
    internal sealed class ReportingEnv
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

        public FakeClock Clock { get; } = new(Now);
        public TestUserAccessContext Access { get; } = new();
        public TestCurrentUser User { get; }
        public FakeScopeHierarchy Hierarchy { get; } = new();
        public FakeAuditWriter Audit { get; } = new();
        public PermissionAuthorizer Authorizer { get; }
        public FakeReportData Data { get; } = new();
        public Guid UserId { get; } = Guid.NewGuid();
        public RequestInfo Request { get; } = new("10.0.0.1", "agent");

        public ReportingEnv()
        {
            User = new TestCurrentUser(Access) { UserId = UserId };
            Authorizer = new PermissionAuthorizer(User, Access, Hierarchy);
        }

        // The signed-in user holds exactly these permissions, in these scopes (Global when none is given).
        public ReportingEnv Grant(IEnumerable<string> permissions, params ScopeGrant[] scopes)
        {
            Access.Set(new UserAccess(UserId, [TestGrants.Role("TestRole", permissions, scopes.Length == 0 ? [new ScopeGrant(ScopeType.Global, null)] : scopes)]));
            return this;
        }

        public ReportHandler Handler(IEnumerable<IReportProvider> providers, params IReportExporter[] exporters)
            => new(Authorizer, new ReportCatalog(providers), exporters, Audit, Clock);
    }




}
