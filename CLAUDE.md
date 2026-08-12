# Engineering notes

Context for anyone (human or AI) making changes to this project.

- The repo holds more than one application. The .NET chat app lives under
  `applications/HindsightChatDemo/`, not at the repo root — `dotnet run`,
  `dotnet build`, etc. must be run from that directory. `docker-compose.hindsight.yml`
  stays at the repo root since both `applications/HindsightChatDemo/` and `explainer/`
  depend on it.
- Hindsight's own LLM backend (used for fact extraction/consolidation) is configured via
  `HINDSIGHT_API_LLM_MODEL` in `docker-compose.hindsight.yml`. Keep it in sync with
  `Ollama:Model` in `applications/HindsightChatDemo/appsettings.json` — they should point
  at the same model.
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
  Ollama version fixes this, `OllamaApiClient` in
  `applications/HindsightChatDemo/Services/HindsightAgentService.cs` can be swapped for
  `OpenAIClient(...).AsIChatClient()`.
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
- The .NET app talks to Hindsight two ways on purpose, not by accident: MCP
  (`Services/HindsightAgentService.cs`) for the LLM-driven write path, and a typed REST
  client (`HindsightClient/`) for the direct read path (`GET /api/memories`, the sidebar's
  "Hafızayı REST'ten oku" button). See "Two ways to talk to Hindsight" in
  [README.md](README.md). Hindsight's `GET /v1/default/banks/{bank_id}/memories/list`
  endpoint isn't in the official docs page — found by reading `/openapi.json` off the
  running container.
- Configuration is bound via the Options pattern (`Configuration/OllamaOptions.cs`,
  `Configuration/HindsightOptions.cs`) with `ValidateOnStart()`, not raw
  `IConfiguration["Section:Key"]` reads. If you add a new setting, add it to the matching
  options class (with a `[Required]`/`[Range]` attribute if it must be present), not as a
  new ad hoc `_config[...]` call.
- `Services/GreetingDetector.cs` short-circuits bare greetings ("merhaba", "selam", ...)
  before they reach the agent. Found by testing: `llama3.1:8b` reliably (4/4) calls
  `retain` with garbled content on a bare greeting, even though the system prompt
  explicitly forbids it. Three rounds of prompt hardening — adding the rule, adding
  concrete examples, moving it to the first paragraph in all caps — all still failed 4/4.
  This is a real model limitation on short/low-information input, not a prompt-wording
  problem; don't try to fix it by editing `system_message.txt` again, it won't work.
  `GreetingDetector` only matches messages that are *entirely* a known greeting, so
  anything with real content ("Merhaba, ben Ahmet, ...") still reaches the agent normally.
- Ollama evicts an idle model from memory after 5 minutes by default, so the first message
  after a pause (e.g. mid-demo, while explaining something) pays a full reload. Fixed by
  setting `OLLAMA_KEEP_ALIVE=30m` for the Ollama app (`launchctl setenv OLLAMA_KEEP_ALIVE
  "30m"`, then restart Ollama.app) — not a project file, a machine-level setting, so it
  needs to be redone on a new machine. Verify with `ollama ps`: the `UNTIL` column should
  show ~30 minutes, not ~5.
- `HindsightAgentService.EnforceMinimumMaxTokens` clamps `recall`/`reflect`'s `max_tokens`
  argument up to 500 before the call goes out. Found by testing: the model sometimes picks
  a tiny value (seen as low as 1 and 10) for that argument, and Hindsight then has no room
  to fit even one fact, returning zero results — the agent then gives a vague "I don't have
  that information" answer that looks like a memory failure but isn't one. Verified directly
  against Hindsight's REST API: the identical query returned 0 results at `max_tokens=10`
  and 1 correct result at `max_tokens=1024`. Same pattern as `GreetingDetector` — a proven
  model quirk fixed in code, not by asking the prompt to behave differently.
- `/api/health` is wired through ASP.NET Core's Health Checks middleware
  (`Endpoints/HealthEndpoints.cs`) with a custom `ResponseWriter`, not a hand-rolled HTTP
  call. Gotcha if you touch it: a `JsonSerializer.Serialize(...)` call written by hand
  does **not** pick up ASP.NET Core's default camelCase policy the way `Results.Ok(...)`
  does — it needs `new JsonSerializerOptions(JsonSerializerDefaults.Web)` passed in
  explicitly, or the response silently reverts to PascalCase and breaks anything (the UI,
  `DEMO.md`'s checklist) expecting `healthy`/`api`/`hindsight` lowercase.
