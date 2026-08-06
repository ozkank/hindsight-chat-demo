# Hindsight Memory Types — Explainer Notebook

A standalone Jupyter notebook that walks through Hindsight's memory model by calling its
REST API directly — no LLM, no agent, no chat UI. It exists for one purpose: teaching how
`retain` turns into World Facts, Observations, Mental Models, and (an open question)
Experience, without the added noise of an LLM deciding when to call a tool.

Use this **before** the main chat app demo, as a "how the engine works" primer. Use the
main app to show "how an LLM-driven agent decides to use these tools in conversation."

## Setup

```bash
cd explainer
python3 -m venv .venv
./.venv/bin/pip install -r requirements.txt
```

Hindsight must already be running (see the main [README](../README.md) — `docker compose
-f ../docker-compose.hindsight.yml up -d`).

## Run

```bash
./.venv/bin/jupyter notebook hindsight_memory_types.ipynb
```

Run the cells top to bottom. Each section pairs a REST call with a note on where to look
in the [Admin UI](http://localhost:9999) (bank: `explainer-demo` — separate from the main
app's `destek-hatti-demo` bank, so the two never mix).

## What it covers

| Section | What you'll see |
|---|---|
| `retain` → World Facts | One call, one fact, extracted with entities |
| Multiple facts + `consolidate` → Observations | Hindsight synthesizing a pattern across several facts |
| Mental Models | A standing, named question that Hindsight keeps answered as facts change |
| Experience | An open question — three methods tried, none worked; the notebook shows the (non-)result honestly |

Re-running the notebook is safe — the first cell clears out `explainer-demo`'s previous
data (memories and the mental model) so counts stay easy to follow each time.
