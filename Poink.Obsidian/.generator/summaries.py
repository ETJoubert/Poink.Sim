"""Builds a one-paragraph summary per file: XML doc, leading comment, interface doc, or a name-based fallback."""

import re
from pathlib import Path

from classification import RAZOR_PAGE_ROUTE
from sources import SourceFile

SUMMARY_MAX_CHARS = 320
XML_SUMMARY = re.compile(r"<summary>(.*?)</summary>", re.DOTALL)
XML_REFERENCE_TAG = re.compile(r"<(?:see|seealso|paramref|typeparamref)\s+(?:cref|name|langword)=\"(?:\w:)?([^\"]+)\"\s*/>")
XML_ANY_TAG = re.compile(r"</?[^>]+>")
LEADING_LINE_COMMENTS = re.compile(r"\A﻿?\s*((?://[^\n]*\n)+)")
LEADING_BLOCK_COMMENT = re.compile(r"\A﻿?\s*/\*(.*?)\*/", re.DOTALL)
LEADING_RAZOR_COMMENT = re.compile(r"\A﻿?\s*(?:@[^\n]*\n\s*)*@\*(.*?)\*@", re.DOTALL)
PASCAL_WORDS = re.compile(r"[A-Z]+(?=[A-Z][a-z]|\d|\b)|[A-Z]?[a-z]+|\d+")


def humanize(identifier: str) -> str:
    return " ".join(word.lower() if not word.isupper() else word for word in PASCAL_WORDS.findall(identifier))


def clean_xml_summary(raw: str) -> str:
    text = re.sub(r"^\s*///\s?", "", raw, flags=re.MULTILINE)
    text = XML_REFERENCE_TAG.sub(lambda m: m.group(1).split(".")[-1], text)
    text = XML_ANY_TAG.sub("", text)
    return " ".join(text.split())


def xml_doc_summary(source: SourceFile) -> str | None:
    if source.path.suffix != ".cs":
        return None
    candidates = [source.stem] + [t for t in source.declared_types if t != source.stem]
    for type_name in candidates:
        pattern = re.compile(
            rf"((?:^[ \t]*///[^\n]*\n)+)(?:^[ \t]*\[[^\n]*\n)*^[^\n]*\b(?:class|struct|interface|enum|record)"
            rf"(?:\s+(?:class|struct))?\s+{re.escape(type_name)}\b",
            re.MULTILINE)
        match = pattern.search(source.text)
        if match and (summary := XML_SUMMARY.search(match.group(1))):
            return clean_xml_summary(summary.group(1))
    return None


def leading_comment_summary(source: SourceFile) -> str | None:
    if source.is_razor:
        match = LEADING_RAZOR_COMMENT.match(source.text)
    elif source.path.suffix in (".js", ".css"):
        match = LEADING_LINE_COMMENTS.match(source.text) or LEADING_BLOCK_COMMENT.match(source.text)
    else:
        match = None
    if not match:
        return None
    body = re.sub(r"^\s*(?://|\*)\s?", "", match.group(1), flags=re.MULTILINE)
    return " ".join(body.split()) or None


def implemented_interface_summary(source: SourceFile, owners: dict[str, list[SourceFile]]) -> str | None:
    base_list = re.search(rf"\b(?:class|record|struct)\s+{re.escape(source.stem)}\b[^{{;]*?:\s*([^{{;]+)", source.text)
    if not base_list:
        return None
    for interface in re.findall(r"\b(I[A-Z]\w*)", base_list.group(1)):
        for owner in owners.get(interface, []):
            if summary := xml_doc_summary(owner):
                return summary
    return None


def generated_summary(source: SourceFile) -> str:
    role, stem = source.role, source.stem
    words = humanize(stem)

    def without(pattern: str) -> str:
        return humanize(re.sub(pattern, "", stem)) or words

    templates = {
        "page": lambda: f"Routable page at `{RAZOR_PAGE_ROUTE.search(source.text).group(1)}` for {without('Page$')}.",
        "component": lambda: f"UI component for {without('Component$')}.",
        "code-behind": lambda: f"Code-behind logic for the {stem} component.",
        "scoped-style": lambda: f"Scoped styles for the {stem} component.",
        "stylesheet": lambda: f"Global stylesheet for {words}.",
        "script": lambda: f"Client-side script for {words}.",
        "interface": lambda: f"Contract for the {humanize(stem[1:])}.",
        "service": lambda: f"Service that handles {without('Service$')}.",
        "repository": lambda: f"Repository that persists and loads {without('Repository$')}.",
        "helper": lambda: f"Helper methods for {without('Helpers?$')}.",
        "extensions": lambda: f"Extension methods for {without('Extensions$')}.",
        "settings": lambda: f"Configuration settings for {without('(Options|Settings|Config)$')}.",
        "enum": lambda: f"Enumeration of {words} values.",
        "model": lambda: f"Models, records and DTOs for {without('(Models?|Dto|Record)$')}.",
    }
    return templates.get(role, lambda: f"Defines {words}.")()


def shorten(summary: str) -> str:
    if len(summary) <= SUMMARY_MAX_CHARS:
        return summary
    cut = summary[:SUMMARY_MAX_CHARS]
    sentence_end = cut.rfind(". ")
    return cut[: sentence_end + 1] if sentence_end > 80 else cut.rsplit(" ", 1)[0] + "…"


def summarize(source: SourceFile, by_path: dict[Path, SourceFile], owners: dict[str, list[SourceFile]]) -> tuple[str, str]:
    summary = xml_doc_summary(source) or leading_comment_summary(source)
    origin = "documented"
    if not summary and source.is_razor:
        code_behind = by_path.get(source.path.with_name(source.name + ".cs"))
        summary = code_behind and xml_doc_summary(code_behind)
    if not summary:
        summary = implemented_interface_summary(source, owners)
        origin = "interface-doc"
    if not summary:
        summary, origin = generated_summary(source), "generated"
    return shorten(summary), origin
