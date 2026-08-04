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
