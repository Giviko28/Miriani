import { ChatWorkspace } from "../../views/ChatWorkspace";

/** The AI assistant inside the admin console, with saved chat history. */
export function AssistantPage() {
  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-semibold">AI Assistant</h1>
      <ChatWorkspace />
    </div>
  );
}
