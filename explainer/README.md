# Hindsight Quickstart Notebook

A Jupyter notebook that mirrors the structure and simplicity of Hindsight's own
[quickstart notebook](https://github.com/vectorize-io/hindsight-cookbook/blob/main/notebooks/01-quickstart.ipynb),
using the official [`hindsight-client`](https://pypi.org/project/hindsight-client/) Python
package — with an original example (a customer support assistant, Ahmet) instead of the
cookbook's.

Use this **before** the main chat app demo, as a "how the engine works" primer, with no
LLM deciding when to call a tool — just direct `retain`/`recall`/`reflect` calls, so every
run behaves the same way. Use the main app afterward to show "how an LLM-driven agent
decides to use these tools mid-conversation."

## Setup

```bash
cd explainer
python3 -m venv .venv
./.venv/bin/pip install -r requirements.txt
```

Hindsight must already be running (see the main [README](../README.md) — `docker compose
-f ../docker-compose.hindsight.yml up -d`).

## Run

Open `hindsight_memory_types.ipynb` in VS Code (or `./.venv/bin/jupyter notebook
hindsight_memory_types.ipynb` for the browser version) and run the cells top to bottom.
Pick the `explainer/.venv` interpreter as the kernel.

## Structure

**Part 1 — Quickstart** (matches the official notebook's scope): connect, `retain`,
`recall`, `reflect`, a short note on memory types, done. This is what to present live.

**Part 2 — Bonus, optional**: goes beyond the quickstart into how `retain` produces
Hindsight's different memory categories. Two honest, reproducible findings from building
it, worth knowing before presenting:

- **Mental Models** sometimes report no relevant information even when clearly-relevant
  World Facts exist in the same bank.
- **Experience** never populated in testing — three different approaches were tried
  (first-person phrasing, a conversation transcript, the Admin UI's own document upload),
  all three landed as World Facts instead.

Bank used: `quickstart-demo`, separate from the main app's `destek-hatti-demo` bank, so
the two never mix. The notebook clears it at the start and deletes it again at the end.
