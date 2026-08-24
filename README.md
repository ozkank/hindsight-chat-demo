# Hindsight Chat Demo

A minimal .NET 9 chat app that demonstrates [Hindsight](https://hindsight.vectorize.io), an
open-source agent-memory system, in a customer-support scenario. An agent built with
[Microsoft Agent Framework](https://github.com/microsoft/agent-framework) uses Hindsight's
three core operations during a conversation — with every tool call surfaced live in the UI:

- `retain` — writes a durable fact to memory.
- `recall` — reads back one specific fact in a later, unrelated session.
- `reflect` — combines multiple facts, observations, and past conversations into a single
  synthesized answer (e.g. "how has this customer's experience been overall?").

## Architecture

Everything runs locally — no cloud LLM, no external API keys.

![Architecture diagram: browser talks HTTP to the HindsightChatDemo Agent (ASP.NET Core + Microsoft Agent Framework), which talks MCP Protocol to Hindsight and native api/chat to Ollama for tool-calling; Hindsight also uses Ollama for its own fact extraction — all inside a single Local boundary.](docs/architecture.svg)

- The **agent** (`applications/HindsightChatDemo/`) is the only piece with custom code. It
  holds the conversation, decides — via the LLM's tool calls — when to invoke `retain`,
  `recall`, or `reflect`, and returns both the reply and the tool-call log to the browser.
- **Ollama** serves two different consumers with the same local model: the agent's own
  chat/tool-calling loop, and Hindsight's internal fact-extraction pipeline (see
  `HINDSIGHT_API_LLM_MODEL` in `docker-compose.hindsight.yml`).
- **Hindsight is reached two different ways, deliberately**, to demo both integration
  styles side by side:
  - **MCP** (Model Context Protocol) — the agent discovers `retain`/`recall`/`reflect` as
    tools at startup and the *LLM* decides when to call them. This is how the chat
    WRITES memory.
  - **Plain REST** — `IHindsightRestClient` (`HindsightClient/`) calls Hindsight's HTTP
    API directly, no MCP and no LLM involved. The sidebar's "Hafızayı REST'ten oku" button
    uses this to READ memory. See "Two ways to talk to Hindsight" below.

## Prerequisites

- .NET 9 SDK
- Docker (for Hindsight)
- [Ollama](https://ollama.com), running locally, with a tool-calling model pulled
  (default: `llama3.1:8b`)

## Quick start

```bash
git clone https://github.com/ozkank/hindsight-chat-demo.git
cd hindsight-chat-demo

docker compose -f docker-compose.hindsight.yml up -d   # starts Hindsight (API/MCP :8888, Admin UI :9999)
cd applications/HindsightChatDemo
dotnet run                                              # starts the app, prints the listening port
```

Open the printed URL in a browser. `GET /api/health` reports whether both Hindsight and the
agent are up.

## Configuration

Everything is driven by `appsettings.json` — no URLs or model names are hardcoded. Settings
are bound to typed, validated options classes (`Configuration/OllamaOptions.cs`,
`Configuration/HindsightOptions.cs`) instead of read ad hoc by key, so a missing or
malformed value fails at startup rather than deep inside a request.

| Key | Description |
|---|---|
| `Ollama:BaseUrl` | Ollama's native API address (no `/v1` — see below) |
| `Ollama:Model` | Must match `HINDSIGHT_API_LLM_MODEL` in `docker-compose.hindsight.yml` |
| `Ollama:Temperature` | Lower values noticeably improve tool-call reliability (see below) |
| `Hindsight:McpEndpoint` | `{bankId}` is replaced with `Hindsight:BankId`; used by the MCP-based agent |
| `Hindsight:BankId` | Hindsight memory namespace; all sessions in this demo share one, so `recall` in a new session can find what `retain` wrote in a previous one |
| `Hindsight:RestBaseUrl` | Base address for `IHindsightRestClient` — same host as `McpEndpoint`, reached directly over HTTP |
| `Hindsight:AdminUiUrl` | Shown in the UI as a link to Hindsight's own Admin dashboard |

### Why the native Ollama API, not the OpenAI-compatible one

The OpenAI-compatible `/v1` endpoint on this project's original test setup (Ollama 0.32.5)
returned tool calls as malformed text inside `content` instead of a structured `tool_calls`
field — the model was calling `retain`/`recall` correctly, but the response format wasn't
recognized as a tool call. Ollama's native `/api/chat` endpoint handled the same request
correctly, so the app connects through
[OllamaSharp](https://github.com/awaescher/OllamaSharp)'s `OllamaApiClient` instead of the
OpenAI SDK. If a newer Ollama version fixes the compat layer, swap it back in
`applications/HindsightChatDemo/Services/HindsightAgentService.cs`.

## Project layout

```
docker-compose.hindsight.yml                     Hindsight (API + MCP + Admin UI, persistent volume)
applications/HindsightChatDemo/
  Program.cs                                     Composition root only: DI, middleware, endpoint mapping
  Configuration/OllamaOptions.cs                 Typed, validated "Ollama" settings
  Configuration/HindsightOptions.cs              Typed, validated "Hindsight" settings
  Endpoints/ChatEndpoints.cs                     POST /api/chat, GET /api/config (the MCP path)
  Endpoints/MemoryEndpoints.cs                   GET /api/memories (the REST path)
  Endpoints/HealthEndpoints.cs                   GET /api/health (wraps ASP.NET Core Health Checks)
  HealthChecks/AgentHealthCheck.cs               Is the MCP agent ready?
  HealthChecks/HindsightRestHealthCheck.cs       Is Hindsight reachable? (via IHindsightRestClient)
  HindsightClient/IHindsightRestClient.cs        Hindsight's REST API, no MCP/LLM in the loop
  HindsightClient/HindsightRestClient.cs         Typed HttpClient implementation
  HindsightClient/Models.cs                      REST response DTOs (health, memory records)
  Services/HindsightAgentService.cs              MCP connection, agent construction, session management
  Services/ToolCallRecorder.cs                   Captures retain/recall/reflect calls per request (AsyncLocal)
  Models/ChatModels.cs                           Chat request/response DTOs
  system_message.txt                             Agent system prompt (retain/recall/reflect rules)
  wwwroot/                                       Chat UI (vanilla HTML/JS/CSS)
explainer/                                       Standalone Jupyter notebooks (no LLM tool-calling in the loop)
```

`POST /api/chat` takes `{ message, userId, sessionId }` and returns
`{ message, toolCalls, sessionId }`, where `toolCalls` lists every retain/recall/reflect
invoked while handling that request — this is what the UI renders as a memory-activity tag
under each message. Sessions are held in memory per process (no database); starting a new
session gets a fresh `AgentSession` but keeps the same Hindsight `bankId`.

### Two ways to talk to Hindsight

The app deliberately shows both integration styles Hindsight supports:

| | MCP (writes) | REST (reads) |
|---|---|---|
| Endpoint | `POST /api/chat` | `GET /api/memories` |
| Code | `Services/HindsightAgentService.cs` | `HindsightClient/HindsightRestClient.cs` |
| Who decides to call it | The LLM, via tool-calling | The caller, directly |
| Framework piece shown | Microsoft Agent Framework + MCP C# SDK | Typed `HttpClient` (`AddHttpClient<TInterface, TImpl>`) |

Click "Hafızayı REST'ten oku" in the sidebar to fetch the bank's most recent memories
straight over HTTP — the same data `retain` just wrote through MCP, read back a completely
different way.

### API surface

- `GET /api/health` — backed by ASP.NET Core's Health Checks middleware
  (`Endpoints/HealthEndpoints.cs`), aggregating `AgentHealthCheck` and
  `HindsightRestHealthCheck` into the same `{ healthy, api, hindsight }` shape the UI and
  `DEMO.md`'s checklist expect.
- `GET /openapi/v1.json` — .NET 9's built-in OpenAPI document generation, useful to browse
  the API shape during a presentation without extra tooling.

## Known limitations

Tool-call reliability with small models is sensitive to the model, the temperature, and
message framing. Three local models were tested with the same battery of prompts
(single-fact `retain`, fresh-session `recall`, fresh-session `reflect`, each repeated 3-4
times):

| Model | `retain` | `recall` | `reflect` fires | `reflect` stays in Turkish |
|---|---|---|---|---|
| `llama3.2:latest` (3B) | reliable | unreliable | unreliable | n/a |
| `qwen2.5:latest` (7B) | reliable | unreliable (~1/8) | reliable | unreliable (leaked Chinese) |
| `llama3.1:8b` (default when `Llm:Provider=Ollama`) | reliable | reliable (3/3) | reliable (3/3) | reliable (3/3) |

`llama3.1:8b` was the clear winner among local models. It is noticeably slower per reply
than `qwen2.5` (occasionally near a minute), which is an acceptable trade-off for a live
demo. `Ollama:Temperature` at `0.3` measurably helped all three models.

Even with `llama3.1:8b`, complex or multi-fact messages (e.g. a complaint and an address
change in one sentence) are less reliable than a single, clearly-stated fact — keep demo
messages to one fact per turn. And `reflect`'s synthesized answer is sometimes generic
rather than grounded in the specific stored fact, even when the tool call itself succeeds.

For a live demo, treat the memory-activity tag as the artifact of record; the model's prose
around it may occasionally be imperfect.

**The app's actual default is now the cloud path, not the table above.** `Llm:Provider`
defaults to `NvidiaNim` with `meta/llama-3.1-8b-instruct` — much faster per reply than any
local model, at a measured reliability cost: in the same battery, `recall` came back as a
real tool call in 2/3 tries (the third emitted the call as plain text); `reflect` fired 3/3
and stayed in Turkish 3/3 but once sent a malformed argument that Hindsight's server
rejected. (An earlier version of this note also flagged `recall` leaking English narration
into the customer-facing answer in 3/3 tries — fixed via a `system_message.txt` rule, see
CLAUDE.md.) Chosen anyway for demo speed — see "The chat backend is now a config switch" in
[CLAUDE.md](CLAUDE.md) for the full results and how to switch back to `Ollama` if
reliability matters more than speed for a given demo. Hindsight's own extraction LLM
(separate from this) stays on local Ollama regardless of `Llm:Provider` — see
`docker-compose.hindsight.yml` — because both free-tier NVIDIA NIM models tested for that
specific job failed outright.

## Demo script

See [DEMO.md](DEMO.md) for the presentation scenario and a pre-demo checklist.

## Quickstart notebooks

[`explainer/`](explainer/) has two Jupyter notebooks using the official `hindsight-client`
Python package — same structure as
[Hindsight's own cookbook](https://github.com/vectorize-io/hindsight-cookbook), with our
own example. They call `retain`/`recall`/`reflect` directly with no LLM deciding when to
use them, so every run behaves the same way. Good as a "how the engine works" primer
before the live chat demo — see [explainer/README.md](explainer/README.md).
