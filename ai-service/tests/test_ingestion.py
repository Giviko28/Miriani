"""Unit tests for document extraction and chunking (no LLM/network required)."""

import io

import pytest
from openpyxl import Workbook

from app.ingestion.extract import UnsupportedFileType, extract_text
from app.ingestion.chunker import chunk_text


def test_extract_txt():
    text = extract_text("notes.txt", b"hello world")
    assert text == "hello world"


def test_extract_xlsx():
    wb = Workbook()
    ws = wb.active
    ws.title = "Sales"
    ws.append(["Product", "Price"])
    ws.append(["Widget", 100])
    buf = io.BytesIO()
    wb.save(buf)

    text = extract_text("sheet.xlsx", buf.getvalue())
    assert "Sheet: Sales" in text
    assert "Widget" in text and "100" in text


def test_extract_unsupported_type():
    with pytest.raises(UnsupportedFileType):
        extract_text("image.png", b"\x89PNG")


def test_chunk_text_splits_long_input():
    text = "sentence number %d. " % 0 + " ".join(f"sentence {i}." for i in range(1, 400))
    chunks = chunk_text(text)
    assert len(chunks) > 1
    assert all(c.strip() for c in chunks)


def test_chunk_text_short_input_single_chunk():
    chunks = chunk_text("a short policy statement.")
    assert chunks == ["a short policy statement."]
