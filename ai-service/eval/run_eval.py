"""RAG evaluation harness.

Ingests a small fixture corpus into an isolated eval organization, runs a labelled
question set, and reports answer accuracy (expected keyword present) and latency against
the project's success criteria: >= 80% accuracy and < 3s typical response.

Run from the ai-service directory (with the venv + Ollama running):
    .venv\\Scripts\\python.exe -m eval.run_eval
"""

import asyncio
import time

from app.rag import service as rag
from app.rag.store import vector_store

EVAL_ORG = "eval-org-0000"

# (filename, access_role, text)
CORPUS = [
    ("remote_policy.txt", 0,
     "Remote Work Policy. Employees may work remotely up to three days per week. "
     "Remote workers must be available on Slack during core hours of 10am to 4pm."),
    ("expenses.txt", 0,
     "Expense Policy. Meal reimbursement is capped at 40 GEL per day during business travel. "
     "Receipts are required for any expense above 20 GEL."),
    ("pto.txt", 0,
     "Leave Policy. Full-time employees receive 24 working days of paid annual leave. "
     "Unused leave of up to 5 days may be carried into the next year."),
    ("security.txt", 0,
     "Security Policy. Passwords must be at least 12 characters and rotated every 90 days. "
     "Two-factor authentication is mandatory for all email accounts."),
    ("comp_bands.txt", 1,  # Manager-only
     "Confidential Compensation Bands. Software Engineer L1 base salary is 18000 to 26000 GEL per month."),
]

# (question, role_level, expected_keyword_lowercase)
QUESTIONS = [
    ("How many days per week can I work remotely?", 0, "three"),
    ("During what hours must remote workers be available?", 0, "10am"),
    ("What is the daily meal reimbursement cap?", 0, "40"),
    ("Do I need a receipt for a 30 GEL expense?", 0, "receipt"),
    ("How many days of annual leave do full-time employees get?", 0, "24"),
    ("How many unused leave days can be carried over?", 0, "5"),
    ("What is the minimum password length?", 0, "12"),
    ("How often must passwords be rotated?", 0, "90"),
    ("Is two-factor authentication required for email?", 0, "two-factor"),
    ("What is the L1 software engineer salary band?", 1, "18000"),
]


async def main() -> None:
    print("Ingesting eval corpus...")
    for i, (name, role, text) in enumerate(CORPUS):
        rag.ingest_document(
            org_id=EVAL_ORG, doc_id=f"eval-{i}", file_name=name, access_role=role,
            data=text.encode("utf-8"),
        )

    correct = 0
    latencies: list[float] = []
    print("\nRunning questions:\n" + "-" * 70)
    for question, role, expected in QUESTIONS:
        start = time.perf_counter()
        result = await rag.answer(org_id=EVAL_ORG, role_level=role, query=question)
        elapsed = time.perf_counter() - start
        latencies.append(elapsed)

        # Normalize away thousands separators/spaces so "18, 000" matches "18000".
        def norm(s: str) -> str:
            return s.lower().replace(",", "").replace(" ", "")

        hit = norm(expected) in norm(result.answer)
        correct += hit
        print(f"[{'PASS' if hit else 'FAIL'}] ({elapsed:4.1f}s) {question}")
        if not hit:
            print(f"        expected '{expected}' in: {result.answer[:100]}")

    n = len(QUESTIONS)
    latencies.sort()
    accuracy = 100 * correct / n
    avg = sum(latencies) / n
    p95 = latencies[int(0.95 * (n - 1))]

    print("-" * 70)
    print(f"Accuracy : {correct}/{n} = {accuracy:.0f}%   (target >= 80%)  -> {'PASS' if accuracy >= 80 else 'FAIL'}")
    print(f"Latency  : avg {avg:.2f}s, p95 {p95:.2f}s   (target < 3s)    -> {'PASS' if p95 < 3 else 'CHECK'}")

    # Clean up eval data so it doesn't pollute the demo collection.
    for i in range(len(CORPUS)):
        vector_store.delete_document(f"eval-{i}")


if __name__ == "__main__":
    asyncio.run(main())
