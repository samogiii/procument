# Deploying the schema RAG onto the LangFlow server

Target setup: LangFlow runs directly on the host, embeddings come from a second `llama-server`
instance, and Chroma lives on the server so nothing depends on Sam's desktop being switched on.

Paths below assume `/opt/procurement`. Change to taste; keep them consistent.

---

## Step 1 — Copy the notes to the server

Copy **only `tableNotes/`**. Do not copy `chroma_db` — it was built with Ollama embeddings on the
Windows box and you are about to rebuild it with llama-server embeddings. Vectors from two
different models are not interchangeable.

```bash
scp -r tableNotes/ ingest_to_chroma.py user@SERVER:/opt/procurement/
```

You should end up with `/opt/procurement/tableNotes/` containing 76 `.txt` files.

---

## Step 2 — Start a llama-server dedicated to embeddings

Use a **separate instance on its own port** from your chat models. Grab an embedding GGUF —
`nomic-embed-text-v1.5` is the direct match for what was already tested (768 dims):

```bash
llama-server -m /models/nomic-embed-text-v1.5.Q8_0.gguf \
  --embedding \
  --host 0.0.0.0 --port 8081 \
  -c 4096 -b 4096 -ub 4096 \
  --pooling mean
```

Three flags matter and are the usual cause of failure:

- **`-b 4096 -ub 4096`** — for embedding models llama.cpp needs the batch size to be at least as
  large as the longest input sequence. The biggest note is ~1,000 tokens; leaving the default
  512 batch produces `input is too large to process` errors on the long notes.
- **`--pooling mean`** — nomic uses mean pooling. Recent llama.cpp reads this from GGUF metadata,
  but setting it explicitly costs nothing and silently-wrong embeddings are hard to debug.
- **`--embedding`** — some builds spell it `--embeddings`. Check `llama-server --help` if it is
  rejected.

Verify it before going further:

```bash
curl -s http://localhost:8081/v1/embeddings \
  -H "Content-Type: application/json" \
  -d '{"input":"sales order","model":"nomic-embed-text"}' | head -c 300
```

You want JSON with an `embedding` array. Confirm the length is **768**:

```bash
curl -s http://localhost:8081/v1/embeddings -H "Content-Type: application/json" \
  -d '{"input":"sales order","model":"nomic-embed-text"}' \
  | python3 -c "import sys,json; print('dims:', len(json.load(sys.stdin)['data'][0]['embedding']))"
```

---

## Step 3 — Build the Chroma DB on the server

```bash
cd /opt/procurement
pip install chromadb openai          # chromadb's OpenAI embedder needs the openai package
EMBEDDING=openai \
OPENAI_BASE=http://localhost:8081/v1 \
OPENAI_MODEL=nomic-embed-text \
python3 ingest_to_chroma.py
```

Expected output:

```
Ingested 76 notes into 'procument_schema' at /opt/procurement/chroma_db
  embedding backend: openai  ->  768 dimensions
  ...
Smoke test - 'how much has each customer paid on their sales orders':
  InvoiceItems.txt      distance=0.36xx
  CustomerPayments.txt  distance=0.36xx
  Invoices.txt          distance=0.37xx
```

**The smoke test is the gate.** If the top hits are payment/invoice notes, embeddings are working.
If they look random — `AuditLogs`, `SatelliteNodes`, `Warehouses` — pooling or batch size is wrong;
fix Step 2 and re-run. Distances will not match the Ollama run exactly, and that is fine; the
*ordering* is what matters.

Make sure the LangFlow process user can read `/opt/procurement/chroma_db`.

---

## Step 3b — Run Chroma in server mode (required, not optional)

Do **not** point LangFlow at the folder directly. Embedded Chroma keeps a process-wide client
cache keyed by the persist-directory string, and opening it from LangFlow's worker threads throws:

```
KeyError: '/opt/procurement/chroma_db'
```

The tool call comes back with `"status": "error"` and the path as its whole message. Running Chroma
as a local HTTP service avoids that cache entirely, and it also decouples your database from
whatever chromadb version happens to be inside LangFlow's virtualenv.

```bash
chroma run --path /opt/procurement/chroma_db --host 127.0.0.1 --port 8000
```

Verify it is serving your collection:

```bash
curl -s http://localhost:8000/api/v2/heartbeat
```

Then confirm the data is visible through the server:

```bash
python3 -c "
import chromadb
c = chromadb.HttpClient(host='localhost', port=8000)
print([x.name for x in c.list_collections()])
print(c.get_collection('procument_schema').count(), 'docs')
"
```

You want `['procument_schema']` and `76 docs`. Bind to `127.0.0.1` unless something off-box needs
it — there is no authentication on this port by default.

Run it under systemd so it survives reboots:

```ini
# /etc/systemd/system/chroma.service
[Unit]
Description=Chroma vector store (Procument schema)
After=network.target

[Service]
ExecStart=/usr/local/bin/chroma run --path /opt/procurement/chroma_db --host 127.0.0.1 --port 8000
Restart=always
User=YOUR_LANGFLOW_USER

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload && sudo systemctl enable --now chroma
```

---

## Step 4 — Wire it into LangFlow

**OpenAI Embeddings component**
- `Model`: `nomic-embed-text`
- `OpenAI API Base` / `Base URL`: `http://localhost:8081/v1`
- `OpenAI API Key`: any non-empty string, e.g. `sk-no-key-required` — llama-server ignores it but
  LangFlow usually requires the field to be filled

*If LangFlow's OpenAI component refuses a non-official base URL*, use the generic **Embedding Model**
component and set its provider to a custom/OpenAI-compatible endpoint, or drop in a Custom
Component wrapping `langchain_openai.OpenAIEmbeddings(base_url=..., api_key=...)`.

**Chroma DB component** — use **server mode** (see Step 3b; embedded mode fails under LangFlow)
- `Collection Name`: `procument_schema`
- `Chroma Server Host`: `localhost`
- `Chroma Server HTTP Port`: `8000`
- `Persist Directory`: **leave empty** — filling both this and the server host conflicts
- `Embedding`: ← from the embeddings component above
- `Number of Results`: **5**
- `Search Type`: `Similarity`
- **Leave `Ingest Data` disconnected** — the DB is already built; connecting it re-ingests and
  creates duplicates that crowd out your top-k

**Make it a tool**
- Enable **Tool Mode** on the Chroma component
- Tool name: `procument_schema`
- Tool description:

```
Search the ProcumentDB schema knowledge base. Returns notes about database
tables (columns, types, keys, relationships), application workflow stages,
business vocabulary, and canonical join paths. Use this before writing ANY
SQL about customers, RFQs, quotes, sales orders, procurement, purchase
orders, suppliers, parts, shipping, warehouses, invoices or payments. Search
using the business terms from the user's question.
```

- Connect the **Toolset** output → your **Agent**'s `Tools` input

**Agent**
- Paste `agent-system-message.txt` into *Agent Instructions* / System Message
- Temperature `0`

---

## Step 5 — Confirm it is actually retrieving

Ask the agent: **"What table is a sales order stored in?"**

The right answer is `Invoices`. That fact appears nowhere in the table's name — it only exists in
the notes — so a correct answer proves the tool is being called and read. If the agent says
"SalesOrders", or hedges, the tool is not wired: check the Tools connection and the system message.

Then ask something multi-table: **"What is our profit margin per part number?"** A good answer
joins `InvoiceItems` to `ProcurementItems` and takes cost from `ProcurementSupplierQuotes` where
`IsSelected = 1`.

---

## Keeping it running

- Run the embedding llama-server under systemd or your process manager so it survives reboots.
  If it is down, the agent's tool calls fail with a connection error.
- **Re-ingest whenever you change the embedding model or edit the notes.** The script deletes and
  rebuilds the collection, so it is safe to re-run.
- Adding a table later: regenerate its note into `tableNotes/`, re-run Step 3. Per-question token
  cost stays flat at roughly 1,600–2,000 tokens regardless of how many tables you add.
