# web — Frontend (React + Vite + TS)

Scaffolded with Vite (`react-ts`). Tailwind + shadcn are added in **Milestone 6 (week 11)**
when the real UI is built. Right now it hosts the integration smoke-test page
(`src/App.tsx`), which calls the .NET API only — never the Python AI service directly.

```bash
npm install
npm run dev      # http://localhost:5173
```

Requires the .NET API running on http://localhost:5080 and the AI service on :8001.
