#!/usr/bin/env python
"""
Ingest tableNotes/ into a persistent Chroma collection — ONE CHUNK PER FILE.

This is the backup path. Prefer ingesting inside LangFlow (see README.md) so that the
same Embedding component is used for ingest and for search. If you do use this script,
the embedding model set here MUST match the Embedding component in your LangFlow query
flow, or search returns garbage.

    pip install chromadb
    python ingest_to_chroma.py

Re-running is safe: the collection is deleted and rebuilt.
"""
import os, sys, glob

NOTES_DIR  = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "tableNotes")
CHROMA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "chroma_db")
COLLECTION = "procument_schema"

# The embedding backend MUST match the Embedding component in LangFlow, and must be
# reachable from wherever LangFlow runs. Set with the EMBEDDING env var:
#
#   ollama   nomic-embed-text via an Ollama server            (768 dims)
#   openai   any OpenAI-compatible /v1/embeddings endpoint,
#            which includes llama-server started with --embedding   (dims vary by model)
#   default  Chroma's built-in all-MiniLM-L6-v2, no server needed   (384 dims)
#
# Changing the backend changes the vector dimensions, so you MUST re-ingest after switching.
EMBEDDING = os.environ.get("EMBEDDING", "ollama")

OLLAMA_URL   = os.environ.get("OLLAMA_URL", "http://localhost:11434")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "nomic-embed-text")

# For llama-server: OPENAI_BASE=http://<server>:8080/v1  (note the /v1 suffix)
OPENAI_BASE  = os.environ.get("OPENAI_BASE", "http://localhost:8080/v1")
OPENAI_MODEL = os.environ.get("OPENAI_MODEL", "nomic-embed-text")
OPENAI_KEY   = os.environ.get("OPENAI_API_KEY", "sk-no-key-required")


def build_embedding_fn():
    from chromadb.utils import embedding_functions
    if EMBEDDING == "ollama":
        return embedding_functions.OllamaEmbeddingFunction(
            url=f"{OLLAMA_URL}/api/embeddings", model_name=OLLAMA_MODEL
        )
    if EMBEDDING == "openai":
        return embedding_functions.OpenAIEmbeddingFunction(
            api_key=OPENAI_KEY, api_base=OPENAI_BASE, model_name=OPENAI_MODEL
        )
    return embedding_functions.DefaultEmbeddingFunction()


def classify(basename):
    """Split notes into kinds so you can filter retrieval by metadata later."""
    if basename.startswith("_FLOW_"):
        return "flowchart"
    if basename.startswith("_REF_"):
        return "reference"
    return "table"


def main():
    import chromadb

    files = sorted(glob.glob(os.path.join(NOTES_DIR, "*.txt")))
    if not files:
        sys.exit(f"No .txt files found in {NOTES_DIR}")

    client = chromadb.PersistentClient(path=CHROMA_DIR)
    try:
        client.delete_collection(COLLECTION)
    except Exception:
        pass
    coll = client.create_collection(
        name=COLLECTION,
        embedding_function=build_embedding_fn(),
        metadata={"hnsw:space": "cosine"},
    )

    ids, docs, metas = [], [], []
    for path in files:
        base = os.path.basename(path)
        text = open(path, encoding="utf-8").read()
        kind = classify(base)
        ids.append(base)                      # stable id = filename, so re-ingest overwrites
        docs.append(text)                     # WHOLE FILE = ONE CHUNK. Do not split.
        metas.append({
            "source": base,
            "kind": kind,
            "name": base[:-4],
            "chars": len(text),
        })

    # Batch to stay well under any server request limit.
    B = 32
    for i in range(0, len(ids), B):
        coll.add(ids=ids[i:i+B], documents=docs[i:i+B], metadatas=metas[i:i+B])

    total_chars = sum(len(d) for d in docs)
    dims = len(coll.get(ids=[ids[0]], include=["embeddings"])["embeddings"][0])
    print(f"Ingested {len(ids)} notes into '{COLLECTION}' at {CHROMA_DIR}")
    print(f"  embedding backend: {EMBEDDING}  ->  {dims} dimensions")
    print(f"  LangFlow MUST use this same embedding model, or search returns nonsense.")
    print(f"  tables={sum(1 for m in metas if m['kind']=='table')} "
          f"flowcharts={sum(1 for m in metas if m['kind']=='flowchart')} "
          f"reference={sum(1 for m in metas if m['kind']=='reference')}")
    print(f"  corpus: {total_chars:,} chars (~{total_chars//4:,} tokens) across {len(ids)} chunks")
    print(f"  average chunk: ~{total_chars//len(ids)//4} tokens  ->  top-4 retrieval ~"
          f"{4*(total_chars//len(ids))//4} tokens of context")

    # Smoke test: prove retrieval returns a small, relevant subset.
    res = coll.query(query_texts=["how much has each customer paid on their sales orders"],
                     n_results=4)
    print("\nSmoke test - 'how much has each customer paid on their sales orders':")
    for src, dist in zip(res["ids"][0], res["distances"][0]):
        print(f"  {src:<45} distance={dist:.4f}")


if __name__ == "__main__":
    main()
