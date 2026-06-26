using PasTechAI.Domain.Entities;

namespace PasTechAI.Domain.Interfaces;

public interface IRoomRepository
{
    Task<List<Room>> GetAllAsync();
    Task<List<RoomBooking>> CheckAvailabilityAsync(int roomId, DateTime start, DateTime end);
    Task<List<RoomBooking>> GetUserBookingsAsync(string userId, int days = 7);
    Task<int> CreateBookingAsync(RoomBooking booking);
    Task<bool> CancelBookingAsync(int bookingId, string userId);
}
