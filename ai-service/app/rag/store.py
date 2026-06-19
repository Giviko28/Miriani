"""Embedded ChromaDB vector store with organization- and role-scoped retrieval."""

from dataclasses import dataclass

import chromadb

from app.config import settings
from app.rag.embeddings import get_embedding_function


@dataclass
class RetrievedChunk:
    text: str
    doc_id: str
    file_name: str
    chunk_index: int
    distance: float


class VectorStore:
    """Wraps a persistent Chroma collection. One collection holds all orgs' chunks;
    every query is filtered by org_id and the caller's role level."""

    def __init__(self) -> None:
        self._client = chromadb.PersistentClient(path=settings.chroma_path)
        self._collection = self._client.get_or_create_collection(
            name=settings.collection_name,
            embedding_function=get_embedding_function(),
            metadata={"hnsw:space": "cosine"},
        )

    def add_chunks(
        self,
        *,
        org_id: str,
        doc_id: str,
        file_name: str,
        access_role: int,
        chunks: list[str],
    ) -> int:
        """Embed and store chunks. Replaces any existing chunks for the same doc_id."""
        self.delete_document(doc_id)

        ids = [f"{doc_id}:{i}" for i in range(len(chunks))]
        metadatas = [
            {
                "org_id": org_id,
                "doc_id": doc_id,
                "file_name": file_name,
                "access_role": access_role,
                "chunk_index": i,
            }
            for i in range(len(chunks))
        ]
        self._collection.add(ids=ids, documents=chunks, metadatas=metadatas)
        return len(chunks)

    def delete_document(self, doc_id: str) -> None:
        self._collection.delete(where={"doc_id": {"$eq": doc_id}})

    def list_doc_ids(self, org_id: str) -> set[str]:
        """Return the distinct doc_ids currently stored for an org."""
        data = self._collection.get(where={"org_id": {"$eq": org_id}}, include=["metadatas"])
        return {str(m.get("doc_id", "")) for m in (data.get("metadatas") or [])}

    def reconcile(self, org_id: str, valid_doc_ids: set[str]) -> list[str]:
        """Delete any of the org's chunks whose doc_id is not in the authoritative set.

        Removes orphaned vectors left behind when a document was deleted from the system
        of record but its embeddings were never purged. Returns the doc_ids removed.
        """
        stored = self.list_doc_ids(org_id)
        orphans = [d for d in stored if d and d not in valid_doc_ids]
        for doc_id in orphans:
            self._collection.delete(where={"$and": [
                {"org_id": {"$eq": org_id}},
                {"doc_id": {"$eq": doc_id}},
            ]})
        return orphans

    def query(self, *, org_id: str, role_level: int, text: str, top_k: int | None = None) -> list[RetrievedChunk]:
        """Return the most relevant chunks the caller is allowed to see.

        A chunk is visible when it belongs to the caller's org and its required
        access_role is at or below the caller's role level (Employee=0 < Manager=1 < Admin=2).
        """
        where = {
            "$and": [
                {"org_id": {"$eq": org_id}},
                {"access_role": {"$lte": role_level}},
            ]
        }
        result = self._collection.query(
            query_texts=[text],
            n_results=top_k or settings.top_k,
            where=where,
        )

        docs = result.get("documents") or [[]]
        metas = result.get("metadatas") or [[]]
        dists = result.get("distances") or [[]]

        chunks: list[RetrievedChunk] = []
        for text_i, meta_i, dist_i in zip(docs[0], metas[0], dists[0]):
            distance = float(dist_i)
            # Drop clearly-irrelevant matches so we don't ground answers on noise.
            if distance > settings.retrieval_max_distance:
                continue
            chunks.append(
                RetrievedChunk(
                    text=text_i,
                    doc_id=str(meta_i.get("doc_id", "")),
                    file_name=str(meta_i.get("file_name", "")),
                    chunk_index=int(meta_i.get("chunk_index", 0)),
                    distance=distance,
                )
            )
        return chunks

    def count(self) -> int:
        return self._collection.count()


# Single shared instance for the process.
vector_store = VectorStore()
