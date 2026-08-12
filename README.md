# Hindsight Chat Demo

A minimal .NET 9 chat app that demonstrates [Hindsight](https://hindsight.vectorize.io), an
open-source agent-memory system, in a customer-support scenario. An agent built with
[Microsoft Agent Framework](https://github.com/microsoft/agent-framework) uses Hindsight's
three core operations during a conversation — with every tool call surfaced live in the UI:

- `retain` — writes a durable fact to memory.
- `recall` — reads back one specific fact in a later, unrelated session.
- `reflect` — combines multiple facts into a single synthesized answer.

## Architecture

Everything runs locally — no cloud LLM, no external API keys.

![Architecture diagram: browser talks HTTP to the HindsightChatDemo Agent (ASP.NET Core + Microsoft Agent Framework), which talks MCP Protocol to Hindsight and native api/chat to Ollama for tool-calling; Hindsight also uses Ollama for its own fact extraction — all inside a single Local boundary.](docs/architecture.svg)

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

Everything is driven by `appsettings.json` — no URLs or model names are hardcoded.

| Key | Description |
|---|---|
| `Ollama:BaseUrl` | Ollama's native API address |
| `Ollama:Model` | Must match `HINDSIGHT_API_LLM_MODEL` in `docker-compose.hindsight.yml` |
| `Ollama:Temperature` | Lower values improve tool-call reliability |
| `Hindsight:McpEndpoint` | `{bankId}` is replaced with `Hindsight:BankId` |
| `Hindsight:BankId` | Hindsight memory namespace; all sessions share one |
| `Hindsight:RestBaseUrl` | Base address for the direct REST read path |
| `Hindsight:AdminUiUrl` | Link to Hindsight's own Admin dashboard, shown in the UI |

## How it works

1. The browser sends a chat message to `POST /api/chat`.
2. The agent reads the message and decides whether it needs `retain`, `recall`, `reflect`,
   or just a plain reply.
3. If a tool is needed, the agent calls it over MCP. Hindsight runs it and returns a result.
4. The agent turns that result into a natural-language reply and sends it back, along with
   a log of every tool call it made.
5. The UI shows the reply plus a colored tag for each tool call — that tag is the actual
   proof memory was written or read.
6. A new session keeps the same Hindsight bank, so `recall`/`reflect` there can still find
   what an earlier session wrote.

## Known limitations

Small local models vary a lot in tool-call reliability. `llama3.1:8b` (the default) was the
most reliable in testing; smaller models frequently skipped `recall`/`reflect` or leaked
another language into the reply. See [CLAUDE.md](CLAUDE.md) for the full comparison and
other model quirks found along the way.

## Demo script

See [DEMO.md](DEMO.md) for the presentation scenario and a pre-demo checklist.

## Quickstart notebooks

[`explainer/`](explainer/) has two Jupyter notebooks using the official `hindsight-client`
Python package, calling `retain`/`recall`/`reflect` directly with no LLM deciding when to
use them — a "how the engine works" primer before the live chat demo. See
[explainer/README.md](explainer/README.md).
