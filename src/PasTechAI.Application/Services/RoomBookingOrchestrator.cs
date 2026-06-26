using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using PasTechAI.Application.Models;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Application.Services;

public class RoomBookingOrchestrator(
    IOllamaClient ollama,
    IRoomRepository roomRepo,
    IChatRepository chatRepo)
{
    private static readonly List<OllamaTool> Tools =
    [
        new OllamaTool("function", new OllamaToolFunction(
            "list_rooms",
            "แสดงรายการห้องประชุมทั้งหมดที่พร้อมใช้งาน",
            new { type = "object", properties = new { }, required = Array.Empty<string>() })),

        new OllamaTool("function", new OllamaToolFunction(
            "check_availability",
            "ตรวจสอบว่าห้องว่างในช่วงเวลาที่ต้องการหรือไม่",
            new
            {
                type = "object",
                properties = new
                {
                    room_id  = new { type = "integer", description = "ID ของห้อง" },
                    date     = new { type = "string",  description = "วันที่ รูปแบบ YYYY-MM-DD" },
                    start_hour = new { type = "integer", description = "ชั่วโมงเริ่มต้น 0-23" },
                    end_hour   = new { type = "integer", description = "ชั่วโมงสิ้นสุด 0-23" }
                },
                required = new[] { "room_id", "date", "start_hour", "end_hour" }
            })),

        new OllamaTool("function", new OllamaToolFunction(
            "book_room",
            "จองห้องประชุม (ตรวจสอบว่าห้องว่างก่อนจอง)",
            new
            {
                type = "object",
                properties = new
                {
                    room_id    = new { type = "integer", description = "ID ของห้อง" },
                    title      = new { type = "string",  description = "หัวข้อการประชุม" },
                    date       = new { type = "string",  description = "วันที่จอง รูปแบบ YYYY-MM-DD" },
                    start_hour = new { type = "integer", description = "ชั่วโมงเริ่มต้น 0-23" },
                    end_hour   = new { type = "integer", description = "ชั่วโมงสิ้นสุด 0-23" }
                },
                required = new[] { "room_id", "title", "date", "start_hour", "end_hour" }
            })),

        new OllamaTool("function", new OllamaToolFunction(
            "get_my_bookings",
            "ดูรายการจองของผู้ใช้ใน 7 วันข้างหน้า",
            new { type = "object", properties = new { }, required = Array.Empty<string>() })),

        new OllamaTool("function", new OllamaToolFunction(
            "cancel_booking",
            "ยกเลิกการจองห้อง",
            new
            {
                type = "object",
                properties = new
                {
                    booking_id = new { type = "integer", description = "ID การจองที่ต้องการยกเลิก" }
                },
                required = new[] { "booking_id" }
            }))
    ];

    public async IAsyncEnumerable<string> ChatAsync(
        ChatRequest req,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var systemPrompt = $"""
            คุณคือ AI ผู้ช่วยจองห้องประชุม ตอบเป็นภาษาไทยกระชับ
            วันและเวลาปัจจุบัน: {now:yyyy-MM-dd HH:mm} (วัน{DayOfWeekThai(now.DayOfWeek)})
            กฎ:
            - ถ้าผู้ใช้ไม่ระบุ room_id ให้ list_rooms ก่อน แล้วถามว่าต้องการห้องไหน
            - ถ้าไม่แน่ใจเรื่องเวลา ให้ถามยืนยันก่อนจอง
            - เมื่อจองสำเร็จให้แสดง booking ID ด้วย
            """;

        var recentMsgs = await chatRepo.GetRecentAsync(req.SessionId, limit: 10);

        var messages = new List<OllamaRawMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        foreach (var m in recentMsgs)
            messages.Add(new OllamaRawMessage { Role = m.Role, Content = m.Content });
        messages.Add(new OllamaRawMessage { Role = "user", Content = req.Message });

        // ── Round 1: tool call detection (non-streaming) ────────────
        var toolResponse = await ollama.ChatWithToolsAsync(req.Model, messages, Tools);

        if (toolResponse.ToolCalls is { Count: > 0 })
        {
            // Add the assistant message that requested tools
            messages.Add(new OllamaRawMessage
            {
                Role = "assistant",
                Content = toolResponse.Content ?? "",
                ToolCalls = toolResponse.ToolCalls
                    .Select(tc => new OllamaRawToolCall
                    {
                        Function = new OllamaRawToolCallFunction
                        {
                            Name = tc.Name,
                            Arguments = tc.Arguments
                        }
                    })
                    .ToList()
            });

            // Execute each tool and append results
            foreach (var tc in toolResponse.ToolCalls)
            {
                var result = await ExecuteToolAsync(tc.Name, tc.Arguments, req.UserId);
                messages.Add(new OllamaRawMessage { Role = "tool", Content = result });
            }
        }
        else if (!string.IsNullOrWhiteSpace(toolResponse.Content))
        {
            // Model answered directly without tools
            var text = toolResponse.Content!;
            yield return text;
            _ = SaveChatAsync(req, text);
            yield break;
        }

        // ── Round 2: stream final response ────────────────────────────
        var fullResponse = new StringBuilder();
        await foreach (var chunk in ollama.StreamChatRawAsync(
            req.Model, messages,
            new OllamaOptions { Temperature = 0.7f, NumCtx = 8192 },
            ct))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        _ = SaveChatAsync(req, fullResponse.ToString());
    }

    private async Task<string> ExecuteToolAsync(string name, JsonElement args, string userId)
    {
        try
        {
            return name switch
            {
                "list_rooms" => await ListRoomsAsync(),
                "check_availability" => await CheckAvailabilityAsync(args),
                "book_room" => await BookRoomAsync(args, userId),
                "get_my_bookings" => await GetMyBookingsAsync(userId),
                "cancel_booking" => await CancelBookingAsync(args, userId),
                _ => $"ไม่รู้จัก tool: {name}"
            };
        }
        catch (Exception ex)
        {
            return $"เกิดข้อผิดพลาด: {ex.Message}";
        }
    }

    private async Task<string> ListRoomsAsync()
    {
        var rooms = await roomRepo.GetAllAsync();
        if (rooms.Count == 0) return "ไม่มีห้องประชุมในระบบ";
        var sb = new StringBuilder("รายการห้องประชุม:\n");
        foreach (var r in rooms)
            sb.AppendLine($"- ID:{r.Id} {r.Name} ({r.Location}) ความจุ {r.Capacity} คน — {r.Description}");
        return sb.ToString();
    }

    private async Task<string> CheckAvailabilityAsync(JsonElement args)
    {
        var roomId   = args.GetProperty("room_id").GetInt32();
        var date     = args.GetProperty("date").GetString()!;
        var startHr  = args.GetProperty("start_hour").GetInt32();
        var endHr    = args.GetProperty("end_hour").GetInt32();
        var start    = DateTime.Parse($"{date} {startHr:00}:00");
        var end      = DateTime.Parse($"{date} {endHr:00}:00");

        var conflicts = await roomRepo.CheckAvailabilityAsync(roomId, start, end);
        if (conflicts.Count == 0)
            return $"ห้อง ID:{roomId} ว่างในช่วง {start:HH:mm}-{end:HH:mm} วันที่ {date}";

        var sb = new StringBuilder($"ห้อง ID:{roomId} ถูกจองแล้วในช่วงเวลาดังกล่าว:\n");
        foreach (var b in conflicts)
            sb.AppendLine($"- {b.Title} ({b.StartTime:HH:mm}-{b.EndTime:HH:mm}) โดย {b.UserId}");
        return sb.ToString();
    }

    private async Task<string> BookRoomAsync(JsonElement args, string userId)
    {
        var roomId   = args.GetProperty("room_id").GetInt32();
        var title    = args.GetProperty("title").GetString()!;
        var date     = args.GetProperty("date").GetString()!;
        var startHr  = args.GetProperty("start_hour").GetInt32();
        var endHr    = args.GetProperty("end_hour").GetInt32();
        var start    = DateTime.Parse($"{date} {startHr:00}:00");
        var end      = DateTime.Parse($"{date} {endHr:00}:00");

        // Check availability first
        var conflicts = await roomRepo.CheckAvailabilityAsync(roomId, start, end);
        if (conflicts.Count > 0)
            return $"ไม่สามารถจองได้ ห้องถูกจองแล้วในช่วง {start:HH:mm}-{end:HH:mm}";

        var rooms = await roomRepo.GetAllAsync();
        var room = rooms.FirstOrDefault(r => r.Id == roomId);
        if (room == null) return $"ไม่พบห้อง ID:{roomId}";

        var booking = new RoomBooking
        {
            RoomId    = roomId,
            RoomName  = room.Name,
            UserId    = userId,
            Title     = title,
            StartTime = start,
            EndTime   = end
        };
        var bookingId = await roomRepo.CreateBookingAsync(booking);
        return $"จองสำเร็จ! Booking ID: {bookingId} — {room.Name} วันที่ {date} เวลา {start:HH:mm}-{end:HH:mm} หัวข้อ: {title}";
    }

    private async Task<string> GetMyBookingsAsync(string userId)
    {
        var bookings = await roomRepo.GetUserBookingsAsync(userId);
        if (bookings.Count == 0)
            return "คุณไม่มีการจองในอีก 7 วันข้างหน้า";

        var sb = new StringBuilder("รายการจองของคุณ:\n");
        foreach (var b in bookings)
            sb.AppendLine($"- ID:{b.Id} {b.RoomName} วัน{b.StartTime:yyyy-MM-dd} {b.StartTime:HH:mm}-{b.EndTime:HH:mm} — {b.Title}");
        return sb.ToString();
    }

    private async Task<string> CancelBookingAsync(JsonElement args, string userId)
    {
        var bookingId = args.GetProperty("booking_id").GetInt32();
        var success = await roomRepo.CancelBookingAsync(bookingId, userId);
        return success
            ? $"ยกเลิกการจอง ID:{bookingId} เรียบร้อยแล้ว"
            : $"ไม่พบการจอง ID:{bookingId} หรือไม่ใช่การจองของคุณ";
    }

    private async Task SaveChatAsync(ChatRequest req, string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return;
        try
        {
            await chatRepo.SaveAsync(req.SessionId, req.UserId, "user", req.Message);
            await chatRepo.SaveAsync(req.SessionId, req.UserId, "assistant", response);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RoomBooking.SaveChat] {ex.Message}");
        }
    }

    private static string DayOfWeekThai(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday    => "อาทิตย์",
        DayOfWeek.Monday    => "จันทร์",
        DayOfWeek.Tuesday   => "อังคาร",
        DayOfWeek.Wednesday => "พุธ",
        DayOfWeek.Thursday  => "พฤหัสบดี",
        DayOfWeek.Friday    => "ศุกร์",
        DayOfWeek.Saturday  => "เสาร์",
        _ => ""
    };
}
