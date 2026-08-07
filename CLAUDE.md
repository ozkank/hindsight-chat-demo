# Engineering notes

Context for anyone (human or AI) making changes to this project.

- Hindsight's own LLM backend (used for fact extraction/consolidation) is configured via
  `HINDSIGHT_API_LLM_MODEL` in `docker-compose.hindsight.yml`. Keep it in sync with
  `Ollama:Model` in `appsettings.json` — they should point at the same model.
- Default model is `llama3.1:8b`, chosen after testing `llama3.2:latest` (3B) and
  `qwen2.5:latest` (7B) with the same prompt battery. `llama3.1:8b` was the only one that
  reliably triggered `recall` and kept `reflect`'s answers in Turkish — see "Known
  limitations" in [README.md](README.md) for the numbers. It is slower per reply than the
  smaller models; that trade-off was accepted for reliability.
- `Ollama:Temperature` defaults to `0.3`. Lower temperature measurably improved tool-call
  reliability for retain/recall with small local models — see "Known limitations" in
  [README.md](README.md).
- The chat client connects to Ollama's **native** `/api/chat` API via
  [OllamaSharp](https://github.com/awaescher/OllamaSharp), not the OpenAI-compatible `/v1`
  endpoint. On this project's original test environment (Ollama 0.32.5), the OpenAI-compat
  layer returned tool calls as malformed text instead of structured `tool_calls`. If a newer
  Ollama version fixes this, `OllamaApiClient` in `Services/HindsightAgentService.cs` can be
  swapped for `OpenAIClient(...).AsIChatClient()`.
- When passing per-call `ChatOptions` via `ChatClientAgentRunOptions`, the agent's `Tools`
  list must be re-specified on that same `ChatOptions` object — otherwise it silently
  overrides (empties) the tools configured at agent construction. See
  [microsoft/agent-framework#1453](https://github.com/microsoft/agent-framework/issues/1453).
- [`explainer/`](explainer/) uses the official `hindsight-client` package (bypassing the
  LLM/agent entirely) to teach `retain`/`recall`/`reflect` with no tool-call randomness in
  the loop. It previously had a "Part 2" digging into Observations/Mental
  Models/Experience; that section was removed (Mental Models sometimes reported no
  information despite clearly relevant facts existing, and Experience never populated
  under any tested approach — real, reproducible findings, but not reliable enough to
  anchor a demo on). Part 2 now covers per-user memory instead: one Hindsight bank per
  customer, proven isolated, plus `document_id` to keep a multi-message conversation as one
  updating record. One nuance worth knowing if you touch that notebook: `recall` always
  returns its best-effort ranked matches from the given bank, even weak ones — it never
  returns an empty list just because nothing is truly relevant. So "prove bank A can't see
  bank B's data" has to check *which* facts came back, not how many.
