# LangFlow RAG over the Procument schema

Goal: let a local LLM answer questions about `ProcumentDB` **without ever reading the whole
schema**. Retrieval pulls the 4 relevant notes instead of all 76.

## What's here

| File | Purpose |
|---|---|
| `../tableNotes/` | 76 knowledge notes — the corpus you embed into Chroma |
| `prompt-template.txt` | Paste this into the LangFlow **Prompt** component |
| `ingest_to_chroma.py` | Backup ingestion script (prefer ingesting inside LangFlow) |

The corpus is 60 table notes + 12 flowchart notes + 4 reference notes.

---

## The token problem, in numbers

| | Chars | ~Tokens |
|---|---|---|
| Whole corpus (all 76 notes) | 96,719 | **~24,200** |
| Average single note | 1,272 | ~318 |
| Largest note (`_REF_03_query_traps.txt`) | 3,927 | ~981 |
| Prompt template | 1,908 | ~477 |

Stuffing the whole schema into every request costs **~24,700 tokens**. With top-4 retrieval:

- **Typical request:** 4 × ~318 = ~1,270 context + 477 prompt + question ≈ **~1,800 tokens**
- **Worst case** (four of the biggest notes): ~3,200 context + 477 ≈ **~3,700 tokens**

That is a **7–13× reduction**, and it stays flat as you add more tables.

---

## The two settings that actually control the token count

Everything else is plumbing. These two decide whether this works:

**1. Do NOT use a text splitter.** Connect the Directory component straight to Chroma.
Each note is already a self-contained atomic unit — one table, or one pipeline stage. Splitting
`Invoices.txt` in half gives you a chunk of column names with no table name attached, which
retrieves badly and reads worse. One file = one chunk = one retrievable fact.

If your LangFlow build forces a splitter into the path, set **chunk size 4000 characters,
overlap 0**. The largest note is 3,927 characters, so every file still survives whole.

**2. Set `Number of Results` to 4** on the Chroma search component. This is the token dial.
k=4 is ~1,300 tokens; k=10 is ~3,200; k=76 is the 24K you are trying to avoid.

---

## Flow A — Ingestion (build the Chroma DB). Run once.

```
[Directory] ──> [Embedding Model] ──> [Chroma DB]
```

**Directory**
- `Path`: `C:\Users\Sam\Desktop\Projects\procument\tableNotes`
- `Types`: `txt`
- `Depth`: `0`
- `Load Hidden`: off — the `_FLOW_` / `_REF_` files start with an underscore, not a dot, so they load fine

**Embedding Model** — for a local setup use **Ollama Embeddings**
- `Model`: `nomic-embed-text` (pull it first: `ollama pull nomic-embed-text`)
- `Base URL`: `http://localhost:11434`

**Chroma DB**
- `Collection Name`: `procument_schema`
- `Persist Directory`: `C:\Users\Sam\Desktop\Projects\procument\langflow\chroma_db`
- Connect **Directory → Ingest Data** and **Embedding → Embedding**
- Leave `Search Query` empty — an empty query means ingest-only

Run the flow. You should get 76 records.

---

## Flow B — RAG query (the one you actually chat with)

```
[Chat Input] ──> [Chroma DB (search)] ──> [Parser] ──┐
                                                      ├──> [Prompt] ──> [Ollama] ──> [Chat Output]
[Chat Input] ─────────────────────────────────────────┘
```

**Chroma DB** — same `Collection Name`, `Persist Directory` and **the same Embedding Model** as Flow A
- `Search Query`: ← from Chat Input
- `Number of Results`: **4**
- `Search Type`: `Similarity`
- Leave `Ingest Data` disconnected

**Parser** (or "Data to Message" / "Parse Data" depending on your build)
- Turns the retrieved `Data` list into one text blob
- `Template`: `{text}`
- `Separator`: a newline or `\n---\n`

**Prompt**
- Paste the full contents of `prompt-template.txt` into `Template`
- Two variables appear automatically: connect **Parser → `context`** and **Chat Input → `question`**

**Ollama** (or LM Studio / any local model component)
- Pick an instruction-following model; SQL generation wants at least a 7B
- `Temperature`: `0` — you want the same question to produce the same SQL
- Make sure the model's context window comfortably exceeds ~4,000 tokens (any modern local model does)

**Chat Output** ← from the model.

---

## Verify it is actually staying small

Ask: *"How much has each customer paid on their sales orders?"*

Open the **Chroma DB** component output. You should see exactly 4 notes, and they should be
payment-related ones such as `Invoices.txt`, `CustomerPayments.txt` and `Customers.txt` — **not**
the other 72. What matters is the count (4, not 76) and that the topic matches; the exact ranking
depends on your embedding model. If you see far more than 4, `Number of Results` is too high or a
splitter is in the path.

Good sign in the answer: the model knows `Invoices` *is* the sales order and joins
`CustomerPayments.InvoiceId`, because that fact is written into `Invoices.txt`.

---

## Tuning k

| k | ~Context tokens | Use when |
|---|---|---|
| 3 | ~950 | Simple single-table lookups |
| **4** | **~1,300** | **Default — start here** |
| 6 | ~1,900 | Questions spanning several stages ("RFQ through to shipping") |
| 10 | ~3,200 | Only if answers are missing tables; check your embeddings first |

If answers miss a table, raising k is the *second* thing to try. First check that the question
uses vocabulary the notes contain — that is what `_REF_01_business_glossary.txt` is for, since it
maps "sales order" onto `Invoices` at retrieval time.

---

## Gotchas

- **The embedding model must be identical in both flows.** Embedding with `nomic-embed-text` and
  searching with something else returns nonsense with no error message. This is the most common
  failure.
- **Re-ingesting after editing a note:** delete the collection first, or you get duplicates that
  crowd out the top-k. The script handles this; in the UI, delete the `chroma_db` folder.
- **`ingest_to_chroma.py` needs `pip install chromadb`** — it is not installed on this machine.
  Only use it if you would rather script the ingest than click through Flow A; the embedding model
  in the script must still match Flow B.
- **This RAG answers questions *about* the schema; it does not run SQL.** To execute the generated
  SQL, add a database component pointed at the read-only `ailogin` account, and keep rule 2 of the
  prompt template (SELECT only) in place.
- Adding a table later: regenerate its note, drop it in `tableNotes/`, re-run Flow A. Nothing else
  changes, and the per-request token cost stays flat.
