# Hindsight Quickstart Notebooks

Two Jupyter notebooks that mirror the structure and simplicity of Hindsight's own
[cookbook](https://github.com/vectorize-io/hindsight-cookbook), using the official
[`hindsight-client`](https://pypi.org/project/hindsight-client/) Python package — with
an original example (a customer support assistant, Ahmet) instead of the cookbook's.

Use these **before** the main chat app demo, as a "how the engine works" primer, with no
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

Open a notebook in VS Code (or `./.venv/bin/jupyter notebook <file>.ipynb` for the browser
version) and run the cells top to bottom. Pick the `explainer/.venv` interpreter as the
kernel.

## Structure

**[`01-quickstart.ipynb`](01-quickstart.ipynb)** (matches the official notebook's scope):
connect, `retain`, `recall`, `reflect`, done. This is what to present live. Bank used:
`quickstart-demo`, deleted again at the end.

**[`02-per-user-memory.ipynb`](02-per-user-memory.ipynb)**: the pattern Hindsight's own
[per-user-memory](https://github.com/vectorize-io/hindsight-cookbook/blob/main/notebooks/02-per-user-memory.ipynb)
example uses — one bank per customer instead of a single shared bank, so one customer's
data can never surface in another's `recall`. Two customer banks (`support-ahmet`,
`support-elif`) prove isolation, then a `document_id`-grouped conversation shows how to
keep a multi-message exchange as one updating record instead of a pile of fragments. Both
banks are deleted at the end.

A note on the isolation-proof cell: `recall` always returns its best-effort ranked
matches from the bank it's given — it doesn't return an empty list just because nothing is
truly relevant. So the notebook doesn't check the result *count*, it checks that none of
the results are the other customer's data. That's the real proof.
