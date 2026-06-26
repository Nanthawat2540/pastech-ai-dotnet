using System.Runtime.CompilerServices;
using System.Text;
using PasTechAI.Application.Models;
using PasTechAI.Domain.Entities;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Application.Services;

public class ChatOrchestrator(
    IOllamaClient ollama,
    IChatRepository chatRepo,
    IMemoryRepository memoryRepo,
    ISummaryRepository summaryRepo,
    IVectorService vector,
    QueryRewriteService queryRewrite,
    MemoryService memoryService,
    SummaryService summaryService)
{
    private const string DefaultSystemPrompt =
        """
        คุณคือ AI Assistant อัจฉริยะ ตอบกระชับ ตรงประเด็น เป็นภาษาไทยหรืออังกฤษตามที่ผู้ใช้ถาม
        กฎสำคัญ:
        - ถ้าไม่แน่ใจให้ถามกลับ
        - ถ้าคำถามกำกวมให้เดาความหมายที่ใกล้เคียงและยืนยันกับผู้ใช้
        - ถ้าค้นข้อมูลไม่พบให้แจ้งว่าไม่พบข้อมูลที่เกี่ยวข้อง
        - ห้ามตอบ "ไม่สามารถช่วยได้" โดยไม่มีเหตุผล
        """;

    public async IAsyncEnumerable<string> ChatAsync(
        ChatRequest req,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // ── Step 1: Query Rewrite ──────────────────────────────────
        var queries = await queryRewrite.RewriteAsync(req.Message, req.UserId);

        // ── Step 2: User Memory ───────────────────────────────────
        var memories = await memoryRepo.GetByUserAsync(req.UserId);

        // ── Step 3: Conversation Summary ──────────────────────────
        var summaries = await summaryRepo.GetRecentByUserAsync(req.UserId, limit: 2);

        // ── Step 4: Vector Search ─────────────────────────────────
        var vectors = await vector.SearchAsync(
            queries,
            types: ["company", "customer", "document", "summary", "chat"],
            userId: null, // ค้นข้ามทุก user สำหรับ company/document
            topK: 5);

        // ── Step 5: Recent Chat ───────────────────────────────────
        var recentMsgs = await chatRepo.GetRecentAsync(req.SessionId, limit: 15);

        // ── Step 6: Build Prompt ──────────────────────────────────
        var systemPrompt = BuildSystemPrompt(req.SystemPrompt, memories, summaries, vectors);
        var ollamaMsgs = BuildMessages(systemPrompt, recentMsgs, req.Message);

        // ── Stream ─────────────────────────────────────────────────
        var fullResponse = new StringBuilder();
        await foreach (var chunk in ollama.StreamChatAsync(
            req.Model, ollamaMsgs,
            new OllamaOptions { Temperature = 0.7f, NumCtx = 8192 },
            ct))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        // ── Post-Process (fire & forget) ──────────────────────────
        _ = PostProcessAsync(req, fullResponse.ToString(), recentMsgs);
    }

    private static string BuildSystemPrompt(
        string customPrompt,
        List<UserMemory> memories,
        List<ConversationSummary> summaries,
        List<VectorResult> vectors)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(customPrompt) ? DefaultSystemPrompt : customPrompt);

        if (memories.Count > 0)
        {
            sb.AppendLine("\n## ข้อมูลผู้ใช้");
            foreach (var m in memories)
                sb.AppendLine($"- {m.MemoryType}: {m.MemoryValue}");
        }

        if (summaries.Count > 0)
        {
            sb.AppendLine("\n## สรุปการสนทนาที่ผ่านมา");
            foreach (var s in summaries)
                sb.AppendLine(s.Summary);
        }

        if (vectors.Count > 0)
        {
            sb.AppendLine("\n## ข้อมูลที่เกี่ยวข้อง");
            foreach (var v in vectors)
                sb.AppendLine($"[{v.Type}] {v.TextPreview}");
        }

        return sb.ToString();
    }

    private static List<OllamaMessage> BuildMessages(
        string systemPrompt,
        List<ChatMessage> recentMsgs,
        string currentMessage)
    {
        var msgs = new List<OllamaMessage>
        {
            new("system", systemPrompt)
        };
        foreach (var m in recentMsgs)
            msgs.Add(new OllamaMessage(m.Role, m.Content));
        msgs.Add(new OllamaMessage("user", currentMessage));
        return msgs;
    }

    private async Task PostProcessAsync(
        ChatRequest req, string response, List<ChatMessage> recentMsgs)
    {
        if (string.IsNullOrWhiteSpace(response)) return;
        try
        {
            await chatRepo.SaveAsync(req.SessionId, req.UserId, "user", req.Message);
            await chatRepo.SaveAsync(req.SessionId, req.UserId, "assistant", response);

            await vector.IndexAsync(
                Guid.NewGuid().ToString(),
                $"Q: {req.Message}\nA: {response}",
                new VectorPayload("chat", UserId: req.UserId));

            var msgCount = await chatRepo.CountAsync(req.SessionId);

            var allMsgs = recentMsgs.Append(new ChatMessage { Role = "user", Content = req.Message })
                                     .Append(new ChatMessage { Role = "assistant", Content = response })
                                     .ToList();

            if (msgCount % 10 == 0)
                await memoryService.ExtractAndSaveAsync(req.UserId, allMsgs);

            if (msgCount % 50 == 0)
                await summaryService.GenerateAndSaveAsync(req.SessionId, req.UserId);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PostProcess] {ex.Message}");
        }
    }
}
