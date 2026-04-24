# Chat History & Conversational Memory — Design

Date: 2026-06-14
Status: Approved (pending spec review)

## Goal

Give every signed-in user a ChatGPT-style assistant: past conversations are saved, listed,
and resumable, and the AI **remembers earlier turns within a conversation** so follow-up
questions work. Today the assistant is fully stateless — `/agent/run` takes one `query` and
returns one answer, with no session or memory anywhere.

Conversation state lives in MS SQL (owned by the .NET gateway); the Python AI service stays
a stateless brain — recent turns are passed to it per request.

## Non-goals (YAGNI)

- Renaming sessions (titles are auto-generated).
- Full-text search across history, sharing/exporting chats, pinning/folders.
- Streaming responses.
- Summarizing or embedding old turns; memory is a fixed window of the most recent turns.
- Cross-device sync beyond what the DB already provides.

## Roles & scope

All signed-in users (Employee, Manager, Admin) get private chat history. A user only ever
sees and resumes their own sessions — every query is filtered by the `UserId` from the JWT.
The same chat workspace is used by the employee chat view and the admin console's AI
Assistant section.

## Data model (MS SQL)

### ChatSession (new table `ChatSessions`)
| Column | Type | Notes |
|--------|------|-------|
| `Id` | Guid (PK) | |
| `OrgId` | Guid | scope |
| `UserId` | Guid (FK → Users) | owner; indexed |
| `Title` | string (≤ 200) | auto from first user message |
| `CreatedAt` | DateTime (UTC) | |
| `UpdatedAt` | DateTime (UTC) | bumped on each new message; sort key |

Index: `(UserId, UpdatedAt)` for the newest-first session list.

### ChatMessage (new table `ChatMessages`)
| Column | Type | Notes |
|--------|------|-------|
| `Id` | Guid (PK) | |
| `SessionId` | Guid (FK → ChatSessions, cascade delete) | indexed |
| `Sender` | string (≤ 16) | `"user"` or `"assistant"` |
| `Content` | string (nvarchar(max)) | the message text / answer |
| `Route` | string? (≤ 32) | assistant only: the agent route |
| `UsedContext` | bool | assistant only: was the answer grounded |
| `Sources` | string? (nvarchar(max)) | assistant only: JSON array of sources |
| `Structured` | string? (nvarchar(max)) | assistant only: JSON structured output |
| `CreatedAt` | DateTime (UTC) | order within a session |

Index: `(SessionId, CreatedAt)`.

Deleting a session cascade-deletes its messages.

## Conversational memory

The Python service's `/agent/run` gains an **optional** `history` field: a list of recent
turns `[{ "sender": "user"|"assistant", "content": "..." }]`. The service stays stateless —
the gateway supplies history on every call.

Gateway behavior per new message:
1. Resolve or create the session.
2. Persist the user message.
3. Load the **last 10 messages** of the session (5 turns) in chronological order, excluding
   the just-added message, as `history`.
4. Call `/agent/run` with `{ org_id, role_level, query, history }`.
5. Persist the assistant reply (with route/usedContext/sources/structured).
6. Bump `Session.UpdatedAt`.

AI-service behavior:
- `AgentState` gains an optional `history` list.
- The **router** classifies on the current `query`; its LLM fallback may also see history so
  pronoun-only follow-ups route sensibly. Default route remains `policy_qa`.
- **Retrieval** still uses the current `query` only, so RAG citations stay accurate.
- **Answer generation** for the text agents (`policy_qa`, `doc_summary`, `email_draft`,
  `report_draft`) prepends a "Conversation so far:" block built from `history` to the LLM
  prompt, so the model can interpret references to earlier turns. `invoice_gen` ignores
  history (structured extraction from the current message).

The change is backward-compatible: omitting `history` reproduces today's behavior.

## Gateway endpoints (.NET, all `[Authorize]`, scoped to the caller's UserId)

- `GET /api/chat/sessions` → `[{ id, title, updatedAt }]`, newest first.
- `GET /api/chat/sessions/{id}` → `{ id, title, messages: [{ id, sender, content, route,
  usedContext, sources, structured, createdAt }] }`. 404 if the session isn't the caller's.
- `POST /api/chat/message` `{ sessionId?: Guid, query: string }` →
  `{ sessionId, title, answer, route, usedContext, sources, structured }`. Creates the
  session when `sessionId` is null (title = trimmed first ~60 chars of the query). 404 if a
  provided `sessionId` isn't the caller's.
- `DELETE /api/chat/sessions/{id}` → 204; cascade-deletes messages. 404 if not the caller's.

The existing `POST /api/ai/agent` stays for one-shot use; the chat UI uses
`POST /api/chat/message`.

## Frontend — ChatGPT-style workspace

New shared component `web/src/views/ChatWorkspace.tsx`, used by both the employee chat view
(`Chat.tsx`) and the admin console (`admin/pages/AssistantPage.tsx`):

- **Left column:** a `New chat` button and the list of the user's sessions (title +
  relative time), newest first; the active session highlighted; a delete (×) per row.
- **Main pane:** the message thread. User messages right-aligned; assistant messages render
  the existing route badge, `grounded` badge, answer text, structured JSON block, and
  sources list (this rendering is extracted from today's `Assistant.tsx` into a
  `MessageBubble`). FAQ suggestion chips show on an empty/new chat and submit on click.
- **Input** at the bottom; submitting calls `POST /api/chat/message` with the current
  `sessionId` (or null for a new chat), appends the user message optimistically, then the
  assistant reply, and refreshes the session list (new session appears / moves to top).

`Assistant.tsx` (single-shot) is replaced by `ChatWorkspace` in both mount points; the
reusable answer rendering moves into `MessageBubble`. `Chat.tsx` keeps its header
(identity, change password, sign out) and hosts the workspace.

API client (`web/src/api.ts`) gains `chat.listSessions`, `chat.getSession`,
`chat.sendMessage`, `chat.deleteSession` and the `ChatSessionSummary` / `ChatMessage` types.

## Testing

- **Gateway:** posting with no `sessionId` creates one session + a user and an assistant
  message; the session title comes from the first query; posting again with the returned
  `sessionId` appends (4 messages) and the AI client receives prior turns as history; a user
  cannot GET/DELETE another user's session (404); delete removes the session and its
  messages. Mirror the existing xUnit + EF-InMemory style; stub the AI client.
- **AI service (pytest):** `/agent/run` returns a grounded answer both without `history` and
  with a short `history`, and a follow-up that relies on history is answered using it.
- **Web:** `tsc`/build clean; manual check of create/resume/delete and a working follow-up.

## Data & migration summary

One EF Core migration adds `ChatSessions` and `ChatMessages`. No changes to existing tables.
`DbInitializer` is unchanged. AI-service change is additive (optional `history`).
