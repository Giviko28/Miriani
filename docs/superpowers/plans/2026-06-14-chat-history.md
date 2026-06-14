# Chat History & Conversational Memory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement
> this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every signed-in user resumable, ChatGPT-style conversations with the AI that
remember earlier turns within a thread.

**Architecture:** Conversation state (sessions + messages) lives in MS SQL, owned by the .NET
gateway; the Python AI service stays stateless and gains an optional `history` field. A new
`POST /api/chat/message` persists each turn, sends recent turns to `/agent/run`, and stores
the reply. The frontend replaces the single-shot Assistant with a two-pane ChatWorkspace.

**Tech Stack:** .NET 10 / EF Core / MS SQL, Python FastAPI / LangGraph, React 18 / TS / Vite.

Spec: `docs/superpowers/specs/2026-06-14-chat-history-design.md`.

---

## File structure

Backend (`api/`):
- `Domain/Entities/ChatSession.cs`, `Domain/Entities/ChatMessage.cs` — new entities.
- `Infrastructure/Persistence/AppDbContext.cs` — DbSets + config.
- `Infrastructure/Persistence/Migrations/*` — one new migration.
- `Application/Chat/ChatContracts.cs`, `Application/Chat/IChatService.cs` — contracts.
- `Application/Ai/AiContracts.cs` — extend `IAiService.RunAgentAsync` with history.
- `Infrastructure/Ai/AiServiceClient.cs` — send `history` to `/agent/run`.
- `Infrastructure/Chat/ChatService.cs` — session/message persistence + AI orchestration.
- `Api/Controllers/ChatController.cs` — REST endpoints.
- `Infrastructure/DependencyInjection.cs` — register `IChatService`.
- `Tests/ChatServiceTests.cs` — xUnit (EF InMemory) with a stub AI service.

AI service (`ai-service/`):
- `app/agents/state.py` — add `history` to `AgentState`.
- `app/main.py` — add `history` to `AgentRequest`, pass through `run_agents`.
- `app/agents/graph.py` — thread `history` into the initial state.
- `app/agents/specialists.py` — include history in text-agent prompts.
- `tests/test_agents.py` — history is accepted and used.

Frontend (`web/`):
- `src/api.ts` — `chat.*` methods + `ChatSessionSummary` / `ChatMessage` types.
- `src/views/MessageBubble.tsx` — render one message (extracted from `Assistant.tsx`).
- `src/views/ChatWorkspace.tsx` — two-pane sessions + thread + input.
- `src/views/Chat.tsx` — host `ChatWorkspace`.
- `src/admin/pages/AssistantPage.tsx` — host `ChatWorkspace`.
- Delete `src/views/Assistant.tsx` (replaced).

---

## Task 1: Chat entities

**Files:**
- Create: `api/Domain/Entities/ChatSession.cs`, `api/Domain/Entities/ChatMessage.cs`

- [ ] `ChatSession`: `Id (Guid)`, `OrgId (Guid)`, `UserId (Guid)`, `Title (string)`,
  `CreatedAt (DateTime)`, `UpdatedAt (DateTime)`, and `List<ChatMessage> Messages = []`.
- [ ] `ChatMessage`: `Id (Guid)`, `SessionId (Guid)`, `ChatSession? Session`,
  `Sender (string)`, `Content (string)`, `Route (string?)`, `UsedContext (bool)`,
  `Sources (string?)`, `Structured (string?)`, `CreatedAt (DateTime)`.
- [ ] Build: `dotnet build api/Domain` → succeeds.
- [ ] Commit: `feat(domain): chat session and message entities`.

## Task 2: DbContext config + migration

**Files:**
- Modify: `api/Infrastructure/Persistence/AppDbContext.cs`

- [ ] Add `DbSet<ChatSession> ChatSessions` and `DbSet<ChatMessage> ChatMessages`.
- [ ] Configure `ChatSession`: `Title` max length 200 required; index `(UserId, UpdatedAt)`.
- [ ] Configure `ChatMessage`: `Sender` max length 16 required; `Content` required (no max →
  nvarchar(max)); `Route` max length 32; one-to-many
  `HasOne(m => m.Session).WithMany(s => s.Messages).HasForeignKey(m => m.SessionId)
  .OnDelete(DeleteBehavior.Cascade)`; index `(SessionId, CreatedAt)`.
- [ ] Stop the running API (free the build lock).
- [ ] `dotnet build api/Bpa.sln` → succeeds.
- [ ] `dotnet ef migrations add ChatHistory --project api/Infrastructure --startup-project api/Api --no-build`.
- [ ] Verify the migration creates `ChatSessions` and `ChatMessages`.
- [ ] Commit: `feat(db): migration for chat sessions and messages`.

## Task 3: AI service — accept and use history

**Files:**
- Modify: `ai-service/app/agents/state.py`, `ai-service/app/main.py`,
  `ai-service/app/agents/graph.py`, `ai-service/app/agents/specialists.py`
- Test: `ai-service/tests/test_agents.py`

- [ ] **Write the failing test** in `tests/test_agents.py`:

```python
import asyncio
from app.agents.graph import run_agents

def test_run_agents_accepts_history(monkeypatch):
    async def fake_generate(prompt, system=None):
        return "ok"
    import app.agents.specialists as sp
    monkeypatch.setattr(sp, "generate", fake_generate)
    history = [{"sender": "user", "content": "hi"}, {"sender": "assistant", "content": "hello"}]
    state = asyncio.run(run_agents(org_id="o", role_level=0, query="write an email", history=history))
    assert state["route"] in ("email_draft", "policy_qa", "report_draft", "doc_summary", "invoice_gen")
    assert "answer" in state
```

- [ ] **Run** `cd ai-service && .venv\Scripts\python.exe -m pytest tests/test_agents.py::test_run_agents_accepts_history -q` → FAIL (`run_agents` has no `history` arg).
- [ ] In `state.py`, add to `AgentState`: `history: list[dict[str, str]]`.
- [ ] In `graph.py`, change `run_agents` signature to
  `async def run_agents(*, org_id, role_level, query, history=None)` and seed the initial
  state with `"history": history or []`.
- [ ] In `specialists.py`, add a helper and use it in the text agents:

```python
def _history_block(state) -> str:
    history = state.get("history") or []
    if not history:
        return ""
    lines = [f"{m['sender'].capitalize()}: {m['content']}" for m in history]
    return "Conversation so far:\n" + "\n".join(lines) + "\n\n"
```

  Prepend `_history_block(state)` to the prompt in `policy_qa`, `doc_summary`,
  `email_draft`, and `report_draft` (before their existing prompt text). Leave
  `invoice_gen` unchanged.
- [ ] In `main.py`, add `history: list[dict] | None = None` to `AgentRequest` and pass
  `history=req.history` into `run_agents(...)`.
- [ ] **Run** the test → PASS. Then run the full suite:
  `.venv\Scripts\python.exe -m pytest -q` → all green.
- [ ] Commit: `feat(ai): optional conversation history in the agent`.

## Task 4: Gateway AI client — forward history

**Files:**
- Modify: `api/Application/Ai/AiContracts.cs`, `api/Infrastructure/Ai/AiServiceClient.cs`

- [ ] In `AiContracts.cs`, define `public record AiTurn(string Sender, string Content);` and
  change the interface method to
  `Task<AiAgentAnswer> RunAgentAsync(Guid orgId, UserRole role, string query,
  IReadOnlyList<AiTurn>? history = null, CancellationToken ct = default);`.
- [ ] In `AiServiceClient.cs`, include `history` in the JSON body sent to `/agent/run`
  (snake_case `sender`/`content` items under `history`). When `history` is null, omit it or
  send `[]`.
- [ ] Update the existing caller in `Api/Controllers/AiController.cs` to pass `history: null`
  (keep the one-shot endpoint behavior).
- [ ] Build `api/Bpa.sln` → succeeds.
- [ ] Commit: `feat(api): forward conversation history to the AI service`.

## Task 5: Chat contracts + service

**Files:**
- Create: `api/Application/Chat/ChatContracts.cs`, `api/Application/Chat/IChatService.cs`,
  `api/Infrastructure/Chat/ChatService.cs`
- Modify: `api/Infrastructure/DependencyInjection.cs`

- [ ] Contracts in `ChatContracts.cs`:

```csharp
namespace Application.Chat;

public record ChatSessionSummary(Guid Id, string Title, DateTime UpdatedAt);

public record ChatMessageDto(
    Guid Id, string Sender, string Content, string? Route, bool UsedContext,
    string? Sources, string? Structured, DateTime CreatedAt);

public record ChatThread(Guid Id, string Title, IReadOnlyList<ChatMessageDto> Messages);

public record SendMessageRequest(Guid? SessionId, string Query);

public record SendMessageResult(
    Guid SessionId, string Title, string Answer, string Route, bool UsedContext,
    string? Sources, string? Structured);
```

- [ ] `IChatService.cs`:

```csharp
namespace Application.Chat;

public interface IChatService
{
    Task<IReadOnlyList<ChatSessionSummary>> ListSessionsAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task<ChatThread?> GetThreadAsync(Guid orgId, Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<SendMessageResult> SendAsync(Guid orgId, Guid userId, Domain.Enums.UserRole role, SendMessageRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid orgId, Guid userId, Guid sessionId, CancellationToken ct = default);
}
```

- [ ] `ChatService.cs` implementing the interface against `AppDbContext` + `IAiService`:
  - `ListSessionsAsync`: sessions where `OrgId == orgId && UserId == userId`, ordered by
    `UpdatedAt` desc, projected to `ChatSessionSummary`.
  - `GetThreadAsync`: load the session (same scope) with messages ordered by `CreatedAt`;
    return null if not found.
  - `SendAsync`:
    1. If `req.SessionId` is null, create a `ChatSession` with
       `Title = Truncate(req.Query, 60)`, `OrgId`, `UserId`, timestamps now; else load it in
       scope (throw `KeyNotFoundException` if missing).
    2. Add a user `ChatMessage` (`Sender="user"`, `Content=req.Query`), `SaveChangesAsync`.
    3. Load the last 10 messages of the session by `CreatedAt` excluding nothing special,
       then take the most recent 10 in chronological order, map to
       `AiTurn(m.Sender, m.Content)` — but exclude the just-added user message from history
       (history is prior turns). Implement by loading messages before the new one.
    4. Call `ai.RunAgentAsync(orgId, role, req.Query, history, ct)`.
    5. Add an assistant `ChatMessage` with `Route`, `UsedContext`, `Sources` (the answer's
       sources serialized to JSON via `System.Text.Json`), `Structured` (JSON or null),
       `Content = answer.Answer`.
    6. Set `session.UpdatedAt = DateTime.UtcNow`; `SaveChangesAsync`.
    7. Return `SendMessageResult` with the session id/title and answer fields.
  - `DeleteAsync`: find the session in scope; if found, remove (cascade) + save, return true;
    else false.
  - Add a private `static string Truncate(string s, int n)` returning the first `n` chars
    (trimmed) with an ellipsis if longer, never empty (fallback `"New chat"`).
- [ ] Register `services.AddScoped<IChatService, ChatService>();` in `DependencyInjection.cs`.
- [ ] Build `api/Bpa.sln` → succeeds.
- [ ] Commit: `feat(api): chat session/message service with history`.

## Task 6: ChatController

**Files:**
- Create: `api/Api/Controllers/ChatController.cs`

- [ ] Controller `[ApiController] [Authorize] [Route("api/chat")]` injecting `IChatService`
  and `ICurrentUser`:
  - `GET sessions` → `Ok(await chat.ListSessionsAsync(currentUser.OrgId, currentUser.UserId, ct))`.
  - `GET sessions/{id:guid}` → thread or `NotFound`.
  - `POST message` `SendMessageRequest` → `BadRequest` if query blank; try
    `SendAsync(...)` → `Ok`; catch `KeyNotFoundException` → `NotFound`; catch
    `HttpRequestException ex` → `Problem($"AI service unavailable: {ex.Message}")`.
  - `DELETE sessions/{id:guid}` → `NoContent` if deleted else `NotFound`.
- [ ] Build `api/Bpa.sln` → succeeds.
- [ ] Run the API; quick curl: login, `POST /api/chat/message {"query":"What is the remote
  work policy?"}` → returns a `sessionId` + grounded answer; `GET /api/chat/sessions` lists
  it; `POST` again with that `sessionId` and a follow-up; `GET /api/chat/sessions/{id}` shows
  4 messages.
- [ ] Commit: `feat(api): chat REST endpoints`.

## Task 7: Gateway tests

**Files:**
- Create: `api/Tests/ChatServiceTests.cs`

- [ ] Add a stub `IAiService` returning a fixed `AiAgentAnswer` and recording the `history`
  it was called with.
- [ ] Tests (EF InMemory, mirroring `UserAdminServiceTests` style):
  - `Send_with_no_session_creates_session_and_two_messages` — after one `SendAsync`, one
    session exists with a user + assistant message; title derived from the query.
  - `Second_send_passes_prior_turns_as_history` — sending again with the returned session id
    calls the AI stub with history containing the first user + assistant messages.
  - `GetThread_returns_null_for_another_users_session` — a session owned by user A is not
    returned for user B.
  - `Delete_removes_session_and_messages` — after delete, the session and its messages are
    gone; deleting a non-owned session returns false.
- [ ] Run `dotnet test api/Tests/Tests.csproj` → all green (existing 17 + new).
- [ ] Commit: `test(api): chat service sessions, history, ownership, delete`.

## Task 8: Frontend api client

**Files:**
- Modify: `web/src/api.ts`

- [ ] Add types:

```ts
export type ChatSessionSummary = { id: string; title: string; updatedAt: string };
export type ChatMessage = {
  id: string; sender: "user" | "assistant"; content: string;
  route: string | null; usedContext: boolean;
  sources: string | null; structured: string | null; createdAt: string;
};
export type ChatThread = { id: string; title: string; messages: ChatMessage[] };
export type SendMessageResult = {
  sessionId: string; title: string; answer: string; route: string;
  usedContext: boolean; sources: string | null; structured: string | null;
};
```

- [ ] Add to the `api` object:

```ts
chat: {
  listSessions: () => request<ChatSessionSummary[]>("/api/chat/sessions"),
  getSession: (id: string) => request<ChatThread>(`/api/chat/sessions/${id}`),
  sendMessage: (sessionId: string | null, query: string) =>
    request<SendMessageResult>("/api/chat/message", jsonBody({ sessionId, query })),
  deleteSession: (id: string) => request<void>(`/api/chat/sessions/${id}`, { method: "DELETE" }),
},
```

- [ ] `npm run build` typechecks. Commit: `feat(web): chat history api client`.

## Task 9: MessageBubble (extract rendering)

**Files:**
- Create: `web/src/views/MessageBubble.tsx`

- [ ] Component `MessageBubble({ message }: { message: ChatMessage })`:
  - User messages: right-aligned bubble with `message.content`.
  - Assistant messages: a card showing the route badge (`ROUTE_LABELS[route] ?? route`), a
    `grounded` badge when `usedContext`, the answer text (`whitespace-pre-wrap`), a
    `<pre>` JSON block when `structured` is non-null (`JSON.parse`), and a Sources list when
    `sources` parses to a non-empty array (parse `sources` as JSON; each item has
    `fileName`, `distance`, `text`). Move `ROUTE_LABELS` here from `Assistant.tsx`.
- [ ] `npm run build` typechecks. Commit: `feat(web): message bubble renderer`.

## Task 10: ChatWorkspace

**Files:**
- Create: `web/src/views/ChatWorkspace.tsx`

- [ ] Component `ChatWorkspace()`:
  - State: `sessions: ChatSessionSummary[]`, `activeId: string | null`,
    `messages: ChatMessage[]`, `input: string`, `busy`, `error`, `faqs: string[]`.
  - On mount: `api.chat.listSessions()` → `sessions`; `api.faqs.list()` → `faqs` (questions).
  - `openSession(id)`: `getSession(id)` → set `activeId` + `messages`.
  - `newChat()`: `activeId=null`, `messages=[]`.
  - `send(text)`: guard non-empty + not busy; optimistic append a user `ChatMessage`
    (temp id, `sender:"user"`, `content:text`); call `api.chat.sendMessage(activeId, text)`;
    append an assistant `ChatMessage` built from the result; set `activeId = result.sessionId`;
    refresh `sessions`. On error set `error` and remove the optimistic user message.
  - `remove(id)`: `deleteSession(id)`; if `id===activeId` reset to new chat; refresh sessions.
  - Layout: left column (`w-60 border-r`) with a `New chat` button and the session list
    (each row: title, click → open, a `×` → remove; highlight `activeId`); right column with
    the scrollable thread of `MessageBubble`, FAQ chips shown only when `messages.length===0`
    (click → `send(question)`), and a bottom input + Send button (disabled while busy).
- [ ] `npm run build` typechecks. Commit: `feat(web): two-pane chat workspace`.

## Task 11: Mount ChatWorkspace; remove Assistant

**Files:**
- Modify: `web/src/views/Chat.tsx`, `web/src/admin/pages/AssistantPage.tsx`
- Delete: `web/src/views/Assistant.tsx`

- [ ] In `Chat.tsx`, replace `<Assistant .../>` with `<ChatWorkspace />` (keep the header and
  the change-password screen toggle). Remove the now-unused FAQ fetching from `Chat.tsx`
  (the workspace owns it).
- [ ] In `AssistantPage.tsx`, replace its body with `<ChatWorkspace />` under the heading;
  remove its FAQ fetching.
- [ ] Delete `web/src/views/Assistant.tsx`.
- [ ] `npm run build` typechecks clean (no dangling imports of `Assistant`).
- [ ] Commit: `feat(web): use chat workspace in user chat and admin console`.

## Task 12: End-to-end verification + docs

- [ ] Ensure services run (per `startup-steps` memory); the dev server picks up changes.
- [ ] In-browser (or via curl if browser tooling is unavailable): start a new chat, ask the
  remote-work policy question (grounded answer), ask a follow-up ("what about for managers?")
  and confirm it is answered in context; reload and confirm the session is listed and
  resumable; delete a session; confirm another user cannot see it.
- [ ] Update `CLAUDE.md` (chat history feature, new tables, `/api/chat/*` endpoints, AI
  `history` field). Commit: `docs: record chat history feature`.

---

## Self-review notes
- Spec coverage: tables (T1–T2), memory/history (T3–T4, T5 step 3), endpoints (T5–T6),
  ownership scoping (T5 GetThread/Delete, T7 tests), workspace UI (T9–T11), AI-service
  additive change (T3), migration (T2), tests (T3 pytest, T7 xUnit). All sections mapped.
- Names consistent across stack: `Sender` ("user"/"assistant"), `UsedContext`, `Route`,
  `Sources`/`Structured` (JSON strings), `sessionId`, `history` `{sender,content}`.
- `Sources`/`Structured` are stored and returned as JSON **strings**; the frontend
  `MessageBubble` parses them (T9), matching the `string | null` types in T8.
