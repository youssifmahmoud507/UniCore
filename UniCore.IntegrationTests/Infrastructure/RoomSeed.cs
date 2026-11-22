using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.University;
using UniCore.Infrastructure.Persistence;

namespace UniCore.IntegrationTests.Infrastructure
{
    // The API has no endpoint for buildings and rooms, so tests add them straight to the database.
    // One university, campus and building are created once per test run; every call adds a new room.
    public static class RoomSeed
    {
        private static readonly SemaphoreSlim BuildingLock = new(1, 1);
        private static Guid _buildingId;

        private static string Short(int length = 8) => Guid.NewGuid().ToString("N")[..length].ToUpperInvariant();

        public static async Task<Guid> RoomAsync(ApiFixture fixture, TestApi api, string admin, int capacity)
        {
            var buildingId = await BuildingAsync(fixture, api, admin);
            var room = Room.Create(buildingId, $"R{Short(8)}", null, capacity, RoomType.ExamHall).Value;

            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Room>().Add(room);
            await db.SaveChangesAsync();

            return room.Id;
        }

        private static async Task<Guid> BuildingAsync(ApiFixture fixture, TestApi api, string admin)
        {
            await BuildingLock.WaitAsync();
            try
            {
                if (_buildingId != Guid.Empty)
                {
                    return _buildingId;
                }

                var university = await api.Client.PostAsync("/api/v1/universities", new { code = $"U{Short(7)}", name = $"Room University {Short(8)}", description = "x", email = "u@uni.test", phone = "0100000000", website = "https://uni.test" }, admin);
                Assert.Equal(201, university.Code);

                var campus = await api.Client.PostAsync("/api/v1/campuses", new
                {
                    universityId = university.GuidOf("id"),
                    code = $"CP{Short(6)}",
                    name = "Test Campus",
                    address = new { street = "1 Main Street", city = "Cairo", state = "Cairo", postalCode = "11511", country = "Egypt" },
                    timeZone = "UTC"
                }, admin);
                Assert.Equal(201, campus.Code);

                var building = Building.Create(campus.GuidOf("id"), $"B{Short(6)}", "Test Building").Value;

                using var scope = fixture.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Set<Building>().Add(building);
                await db.SaveChangesAsync();

                _buildingId = building.Id;
                return _buildingId;
            }
            finally
            {
                BuildingLock.Release();
            }
        }
    }
}
