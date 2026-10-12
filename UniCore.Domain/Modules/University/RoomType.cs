namespace UniCore.Domain.Modules.University
{
    // Stored as text in the database (see AppDbContext conventions), so reordering is safe.
    public enum RoomType
    {
        Classroom = 0,
        LectureHall = 1,
        Laboratory = 2,
        ComputerLab = 3,
        Auditorium = 4,
        ExamHall = 5
    }
}
