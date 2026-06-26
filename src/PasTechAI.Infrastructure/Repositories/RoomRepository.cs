using Dapper;
using Microsoft.Data.SqlClient;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Repositories;

public class RoomRepository(string connStr) : IRoomRepository
{
    public async Task<List<Room>> GetAllAsync()
    {
        using var conn = new SqlConnection(connStr);
        var rooms = await conn.QueryAsync<Room>(
            "SELECT * FROM Rooms WHERE IsActive = 1 ORDER BY Id");
        return rooms.ToList();
    }

    public async Task<List<RoomBooking>> CheckAvailabilityAsync(int roomId, DateTime start, DateTime end)
    {
        using var conn = new SqlConnection(connStr);
        var bookings = await conn.QueryAsync<RoomBooking>("""
            SELECT * FROM RoomBookings
            WHERE RoomId = @roomId
              AND Status = 'confirmed'
              AND NOT (@end <= StartTime OR @start >= EndTime)
            ORDER BY StartTime
            """, new { roomId, start, end });
        return bookings.ToList();
    }

    public async Task<List<RoomBooking>> GetUserBookingsAsync(string userId, int days = 7)
    {
        using var conn = new SqlConnection(connStr);
        var bookings = await conn.QueryAsync<RoomBooking>("""
            SELECT * FROM RoomBookings
            WHERE UserId = @userId
              AND StartTime >= GETDATE()
              AND StartTime <= DATEADD(day, @days, GETDATE())
              AND Status = 'confirmed'
            ORDER BY StartTime
            """, new { userId, days });
        return bookings.ToList();
    }

    public async Task<int> CreateBookingAsync(RoomBooking booking)
    {
        using var conn = new SqlConnection(connStr);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO RoomBookings (RoomId, RoomName, UserId, Title, StartTime, EndTime, Status, CreatedAt)
            VALUES (@RoomId, @RoomName, @UserId, @Title, @StartTime, @EndTime, 'confirmed', GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, booking);
    }

    public async Task<bool> CancelBookingAsync(int bookingId, string userId)
    {
        using var conn = new SqlConnection(connStr);
        var rows = await conn.ExecuteAsync("""
            UPDATE RoomBookings
            SET Status = 'cancelled'
            WHERE Id = @bookingId AND UserId = @userId AND Status = 'confirmed'
            """, new { bookingId, userId });
        return rows > 0;
    }
}
