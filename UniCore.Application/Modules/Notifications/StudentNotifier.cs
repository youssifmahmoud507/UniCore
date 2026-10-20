using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Notifications
{
    // Finds the login account of a student. A student without an account has nobody to notify.
    public sealed class StudentNotifier(INotificationPublisher publisher, IAggregateRepository<Student> students)
    {
        private readonly INotificationPublisher _publisher = publisher;
        private readonly IAggregateRepository<Student> _students = students;

        public async Task<Result> NotifyAsync(Guid studentId, string type, string title, string content, string referenceType, Guid referenceId, Guid eventId, CancellationToken cancellationToken)
        {
            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);

            if (student?.UserId is not { } userId)
            {
                return Result.Ok();
            }

            var sent = await _publisher.PublishAsync(new NotificationRequest(userId, type, title, content, referenceType, referenceId, eventId), cancellationToken);

            return sent.IsSuccess ? Result.Ok() : Result.Fail(sent.Errors);
        }
    }


}
