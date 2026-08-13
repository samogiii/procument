#!/usr/bin/env python
"""
Test the Chroma collection the same way LangFlow does, so you can tell whether a
failure is in Chroma, in the embeddings endpoint, or in the LangFlow wiring.

Run it ON THE SERVER, with the same values you typed into the LangFlow components.

    # embedded mode (matches a Persist Directory in LangFlow)
    CHROMA_PATH=/opt/procurement/chroma_db \
    OPENAI_BASE=http://192.168.29.3:8081/v1 \
    python3 test_chroma.py

    # server mode (matches Chroma Server Host + HTTP Port in LangFlow)
    CHROMA_HOST=localhost CHROMA_PORT=8000 \
    OPENAI_BASE=http://192.168.29.3:8081/v1 \
    python3 test_chroma.py
"""
import os, sys

CHROMA_PATH  = os.environ.get("CHROMA_PATH", "/opt/procurement/chroma_db")
CHROMA_HOST  = os.environ.get("CHROMA_HOST")          # set this to use server mode
CHROMA_PORT  = int(os.environ.get("CHROMA_PORT", "8000"))
COLLECTION   = os.environ.get("COLLECTION", "procument_schema")

OPENAI_BASE  = os.environ.get("OPENAI_BASE", "http://localhost:8081/v1")
OPENAI_MODEL = os.environ.get("OPENAI_MODEL", "nomic-embed-text")
OPENAI_KEY   = os.environ.get("OPENAI_API_KEY", "sk-no-key-required")

QUERIES = [
    "what table is a sales order stored in",
    "how much has each customer paid on their sales orders",
    "profit margin per part number",
]


def ok(m):   print(f"  [OK]   {m}")
def bad(m):  print(f"  [FAIL] {m}")


def main():
    print("=" * 68)
    print("STEP 1 - embeddings endpoint")
    print("=" * 68)
    print(f"  base={OPENAI_BASE}  model={OPENAI_MODEL}")
    try:
        from chromadb.utils import embedding_functions
        ef = embedding_functions.OpenAIEmbeddingFunction(
            api_key=OPENAI_KEY, api_base=OPENAI_BASE, model_name=OPENAI_MODEL)
        dims = len(ef(["sales order"])[0])
        ok(f"embeddings reachable, {dims} dimensions")
    except Exception as e:
        bad(f"{type(e).__name__}: {e}")
        print("\n  -> The embedding server is unreachable or not serving an embedding model.")
        print("     Check the llama-server started with --embedding is up on that host/port.")
        return 1

    print()
    print("=" * 68)
    print("STEP 2 - connect to Chroma")
    print("=" * 68)
    import chromadb
    try:
        if CHROMA_HOST:
            print(f"  server mode: http://{CHROMA_HOST}:{CHROMA_PORT}")
            client = chromadb.HttpClient(host=CHROMA_HOST, port=CHROMA_PORT)
        else:
            print(f"  embedded mode: {CHROMA_PATH}")
            if not os.path.isdir(CHROMA_PATH):
                bad(f"directory does not exist: {CHROMA_PATH}")
                print("\n  -> This is the #1 cause of an empty/erroring tool in LangFlow.")
                print("     Find the real one:  find / -name chroma.sqlite3 2>/dev/null")
                return 1
            client = chromadb.PersistentClient(path=CHROMA_PATH)
        names = [c.name for c in client.list_collections()]
        ok(f"connected. collections: {names}")
    except Exception as e:
        bad(f"{type(e).__name__}: {e}")
        return 1

    if COLLECTION not in names:
        bad(f"collection '{COLLECTION}' NOT FOUND here")
        print("\n  -> Chroma is pointing at the wrong location, or the ingest never ran.")
        return 1

    print()
    print("=" * 68)
    print("STEP 3 - collection contents")
    print("=" * 68)
    coll = client.get_collection(COLLECTION, embedding_function=ef)
    n = coll.count()
    if n == 76:
        ok(f"{n} documents (expected 76)")
    elif n == 0:
        bad("0 documents - the collection exists but is EMPTY. Re-run ingest_to_chroma.py")
        return 1
    else:
        print(f"  [WARN] {n} documents, expected 76. Duplicates or a partial ingest?")

    stored = len(coll.get(ids=[coll.get(limit=1)['ids'][0]], include=["embeddings"])["embeddings"][0])
    if stored == dims:
        ok(f"stored vectors are {stored}-dim, matching the query embedder")
    else:
        bad(f"DIMENSION MISMATCH: stored={stored}, query embedder={dims}")
        print("\n  -> The DB was built with a different embedding model than you are querying with.")
        print("     Re-run ingest_to_chroma.py using the SAME model LangFlow is configured for.")
        return 1

    print()
    print("=" * 68)
    print("STEP 4 - retrieval quality")
    print("=" * 68)
    bad_hits = 0
    for q in QUERIES:
        r = coll.query(query_texts=[q], n_results=5)
        ids, ds = r["ids"][0], r["distances"][0]
        print(f"\n  Q: {q}")
        for i, dist in zip(ids, ds):
            print(f"     {i:<46} {dist:.4f}")
        if ds[0] > 0.75:
            bad_hits += 1
            print("     [WARN] top distance > 0.75 - embeddings may be mismatched")

    print()
    print("=" * 68)
    if bad_hits:
        print("RESULT: connected, but retrieval looks weak. Check the embedding model.")
        return 1
    print("RESULT: Chroma is healthy. If LangFlow still fails, the problem is in the")
    print("        component config - compare these env values to the UI fields exactly.")
    print("=" * 68)
    return 0


if __name__ == "__main__":
    sys.exit(main())
