"""
LangFlow Custom Component — embeddings for llama-server (or any OpenAI-compatible server).

WHY THIS EXISTS
---------------
LangFlow's stock "OpenAI Embeddings" component uses LangChain's OpenAIEmbeddings with
check_embedding_ctx_length left ON. In that mode LangChain does not send your text to the
server at all — it tokenises locally with tiktoken (cl100k_base) and sends ARRAYS OF INTEGER
TOKEN IDS as the `input` field.

OpenAI's own API accepts token IDs. llama-server does not: it tries to map those IDs onto the
embedding model's own vocabulary (nomic-embed-text has ~30k tokens, cl100k IDs run to ~100k),
so out-of-range IDs come back as:

    Error code: 400 - {'error': {'code': 400, 'message': 'Prompt contains invalid tokens', ...}}

Setting check_embedding_ctx_length=False makes LangChain skip tokenisation entirely and POST the
raw strings, which is what llama-server expects — and what chromadb's own embedder does, so the
query vectors finally match the document vectors already in Chroma.

INSTALL
-------
1. LangFlow sidebar -> New Custom Component  (or + -> Custom Component)
2. Paste this whole file into the code editor and save
3. It appears as "llama-server Embeddings". Drag it in, set Base URL and Model
4. Connect its Embeddings output -> the Chroma DB component's Embedding input
5. Delete the old OpenAI Embeddings component so nothing reconnects to it
6. Restart LangFlow, and keep should_cache_vector_store = False on the Chroma component

No re-ingest is needed: ingest_to_chroma.py already sent raw text, so the stored vectors are
built the same way this component now queries them.
"""

from langflow.custom import Component
from langflow.field_typing import Embeddings
from langflow.io import MessageTextInput, Output, SecretStrInput


class LlamaServerEmbeddingsComponent(Component):
    display_name = "llama-server Embeddings"
    description = "OpenAI-compatible embeddings that POST raw text instead of tiktoken token IDs."
    icon = "binary"
    name = "LlamaServerEmbeddings"

    inputs = [
        MessageTextInput(
            name="base_url",
            display_name="Base URL",
            value="http://192.168.29.3:8081/v1",
            info="llama-server address, including the /v1 suffix.",
        ),
        MessageTextInput(
            name="model",
            display_name="Model",
            value="nomic-embed-text",
            info="Ignored by llama-server (it serves whatever GGUF is loaded), but keep it accurate.",
        ),
        SecretStrInput(
            name="api_key",
            display_name="API Key",
            value="sk-no-key-required",
            required=False,
            info="llama-server ignores this, but the OpenAI client requires a non-empty value.",
        ),
    ]

    outputs = [
        Output(display_name="Embeddings", name="embeddings", method="build_embeddings"),
    ]

    def build_embeddings(self) -> Embeddings:
        from langchain_openai import OpenAIEmbeddings

        return OpenAIEmbeddings(
            model=self.model or "nomic-embed-text",
            base_url=self.base_url,
            api_key=self.api_key or "sk-no-key-required",
            # THE ENTIRE POINT OF THIS COMPONENT.
            # False  -> POST the raw strings (llama-server understands this)
            # True   -> POST tiktoken integer token IDs (llama-server rejects them with a 400)
            # It also means tiktoken is never imported, so the offline cache stops mattering.
            check_embedding_ctx_length=False,
        )
