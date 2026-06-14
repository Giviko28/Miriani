import { useEffect, useState } from "react";
import { api } from "../../api";
import { Assistant } from "../../views/Assistant";

/** The AI assistant inside the admin console, with the org's FAQ suggestions. */
export function AssistantPage() {
  const [faqs, setFaqs] = useState<string[]>([]);

  useEffect(() => {
    api.faqs.list().then((l) => setFaqs(l.map((f) => f.question))).catch(() => setFaqs([]));
  }, []);

  return (
    <div className="space-y-5">
      <h1 className="text-2xl font-semibold">AI Assistant</h1>
      <Assistant suggestions={faqs.length > 0 ? faqs : undefined} sendOnClick={faqs.length > 0} />
    </div>
  );
}
