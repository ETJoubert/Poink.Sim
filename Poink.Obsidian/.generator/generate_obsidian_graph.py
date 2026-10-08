"""Regenerates the Poink.Obsidian vault: one note per Poink.Sim source file, wikilinked to the files it uses.

Run from anywhere: python Poink.Obsidian/.generator/generate_obsidian_graph.py
"""

import re
import shutil
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / "Poink.Sim"
VAULT_ROOT = REPO_ROOT / "Poink.Obsidian"
SOURCE_EXTENSIONS = (".cs", ".razor", ".css", ".js", ".html")
EXCLUDED_DIRS = {"bin", "obj", "logs", "Properties"}
VAULT_RESERVED = {".obsidian", ".generator"}
SUMMARY_MAX_CHARS = 320

TYPE_DECLARATION = re.compile(
    r"\b(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?|delegate\s+[\w<>\[\],.? ]+?)\s+([A-Z]\w*)")
CODE_NOISE = re.compile(
    r"//[^\n]*|/\*.*?\*/|@\*.*?\*@|\"(?:\\.|[^\"\\\n])*\"|'(?:\\.|[^'\\\n])'", re.DOTALL)
COMMENTS_ONLY = re.compile(
    r"\"(?:\\.|[^\"\\\n])*\"|(?<![:\w])//[^\n]*|/\*.*?\*/|@\*.*?\*@|<!--.*?-->", re.DOTALL)
MARKUP_TEXT_NODE = re.compile(r">([^<>@{}]*)<")
TYPE_REFERENCE = re.compile(r"(?<![\w.])([A-Z]\w*)")
EXTENSION_METHOD_DECLARATION = re.compile(r"\bstatic\s+[^=;{}()]+?\s+(\w+)\s*(?:<[^>()]*>)?\s*\(\s*this\s")
MEMBER_CALL = re.compile(r"\.([A-Z]\w*)\s*(?:<[^>()]*>)?\s*\(")
XML_SUMMARY = re.compile(r"<summary>(.*?)</summary>", re.DOTALL)
XML_REFERENCE_TAG = re.compile(r"<(?:see|seealso|paramref|typeparamref)\s+(?:cref|name|langword)=\"(?:\w:)?([^\"]+)\"\s*/>")
XML_ANY_TAG = re.compile(r"</?[^>]+>")
RAZOR_PAGE_ROUTE = re.compile(r"^@page\s+\"([^\"]+)\"", re.MULTILINE)
LEADING_LINE_COMMENTS = re.compile(r"\A﻿?\s*((?://[^\n]*\n)+)")
LEADING_BLOCK_COMMENT = re.compile(r"\A﻿?\s*/\*(.*?)\*/", re.DOTALL)
LEADING_RAZOR_COMMENT = re.compile(r"\A﻿?\s*(?:@[^\n]*\n\s*)*@\*(.*?)\*@", re.DOTALL)
PASCAL_WORDS = re.compile(r"[A-Z]+(?=[A-Z][a-z]|\d|\b)|[A-Z]?[a-z]+|\d+")

ROLE_COLORS = {
    "page": 0xE0457B,
    "component": 0xF29E4C,
    "code-behind": 0xEFD35B,
    "scoped-style": 0x9C8F5B,
    "stylesheet": 0x8E8E8E,
    "script": 0xF7DF1E,
    "service": 0x4C9AF2,
    "interface": 0x7FC8F8,
    "repository": 0x8F5BD6,
    "model": 0x4CC38A,
    "enum": 0x2E8B57,
    "helper": 0x26C6DA,
    "extensions": 0x00897B,
    "settings": 0xB0BEC5,
    "vendor": 0x555555,
    "class": 0xCCCCCC,
}


@dataclass
class SourceFile:
    path: Path
    text: str
    role: str = "class"
    declared_types: list[str] = field(default_factory=list)
    uses: set["SourceFile"] = field(default_factory=set)

    def __hash__(self):
        return hash(self.path)

    @property
    def relative(self) -> Path:
        return self.path.relative_to(SOURCE_ROOT)

    @property
    def name(self) -> str:
        return self.path.name

    @property
    def stem(self) -> str:
        return self.path.name.split(".")[0]

    @property
    def note_link_path(self) -> str:
        return self.relative.as_posix() + ".md"

    @property
    def is_code(self) -> bool:
        return self.path.suffix in (".cs", ".razor")

    @property
    def is_asset(self) -> bool:
        return self.path.suffix in (".css", ".js", ".html") and not self.name.endswith(".razor.css")


def discover_sources() -> list[SourceFile]:
    sources = []
    for path in sorted(SOURCE_ROOT.rglob("*")):
        relative_parts = path.relative_to(SOURCE_ROOT).parts
        if not path.is_file() or path.suffix not in SOURCE_EXTENSIONS:
            continue
        if any(part in EXCLUDED_DIRS for part in relative_parts[:-1]):
            continue
        sources.append(SourceFile(path, path.read_text(encoding="utf-8-sig", errors="replace")))
    return sources


def strip_noise(text: str, is_markup: bool) -> str:
    def keep_identifier_like_strings(match: re.Match) -> str:
        token = match.group(0)
        if token.startswith('"') and not re.search(r"\s", token):
            return token
        return " "

    cleaned = CODE_NOISE.sub(keep_identifier_like_strings, text)
    return MARKUP_TEXT_NODE.sub("><", cleaned) if is_markup else cleaned


def strip_comments(text: str) -> str:
    return COMMENTS_ONLY.sub(lambda m: m.group(0) if m.group(0).startswith('"') else " ", text)


def is_vendor(source: SourceFile) -> bool:
    return ".min." in source.name or "lib" in source.relative.parts or source.stem.lower() == "bootstrap"


def classify(source: SourceFile) -> str:
    name, stem = source.name, source.stem
    if is_vendor(source):
        return "vendor"
    if name.endswith(".razor"):
        return "page" if RAZOR_PAGE_ROUTE.search(source.text) else "component"
    if name.endswith(".razor.cs"):
        return "code-behind"
    if name.endswith(".razor.css"):
        return "scoped-style"
    if source.path.suffix == ".css":
        return "stylesheet"
    if source.path.suffix in (".js", ".html"):
        return "script"
    if re.fullmatch(r"I[A-Z]\w*", stem) and re.search(rf"\binterface\s+{stem}\b", source.text):
        return "interface"
    for suffix, role in (("Service", "service"), ("Repository", "repository"), ("Helper", "helper"),
                         ("Helpers", "helper"), ("Extensions", "extensions"), ("Options", "settings"),
                         ("Settings", "settings"), ("Config", "settings"), ("Models", "model"),
                         ("Model", "model"), ("Dto", "model"), ("Record", "model")):
        if stem.endswith(suffix):
            return role
    if source.declared_types and all(re.search(rf"\benum\s+{t}\b", source.text) for t in source.declared_types):
        return "enum"
    if "Models" in source.relative.parts:
        return "model"
    return "class"


def declared_types(source: SourceFile) -> list[str]:
    if source.name.endswith(".razor"):
        return [source.stem]
    if source.path.suffix != ".cs":
        return []
    return list(dict.fromkeys(TYPE_DECLARATION.findall(strip_noise(source.text, False))))


def build_type_owners(sources: list[SourceFile]) -> dict[str, list[SourceFile]]:
    declarers: dict[str, list[SourceFile]] = {}
    for source in sources:
        for type_name in source.declared_types:
            declarers.setdefault(type_name, []).append(source)

    owners = {}
    for type_name, files in declarers.items():
        preferred = ([f for f in files if f.name == f"{type_name}.razor"]
                     or [f for f in files if f.name == f"{type_name}.cs"]
                     or files)
        owners[type_name] = preferred
    return owners


def build_extension_method_owners(sources: list[SourceFile]) -> dict[str, list[SourceFile]]:
    owners: dict[str, list[SourceFile]] = {}
    for source in sources:
        if source.path.suffix == ".cs":
            for method_name in set(EXTENSION_METHOD_DECLARATION.findall(strip_noise(source.text, False))):
                owners.setdefault(method_name, []).append(source)
    return owners


def link_sources(sources: list[SourceFile]) -> None:
    owners = build_type_owners(sources)
    extension_owners = build_extension_method_owners(sources)
    assets = [s for s in sources if s.is_asset]

    for source in sources:
        uncommented = strip_comments(source.text)
        if source.is_code:
            code = strip_noise(source.text, source.path.suffix == ".razor")
            for type_name in set(TYPE_REFERENCE.findall(code)) - set(source.declared_types):
                source.uses.update(owners.get(type_name, []))
            for method_name in set(MEMBER_CALL.findall(code)):
                source.uses.update(extension_owners.get(method_name, []))
            if source.stem in source.declared_types:
                source.uses.update(owners.get(source.stem, []))

        for asset in assets:
            if re.search(rf"(?<![\w.-]){re.escape(asset.name)}\b", uncommented):
                source.uses.add(asset)

        if source.name.endswith((".razor.cs", ".razor.css")):
            component = source.path.with_name(f"{source.stem}.razor")
            source.uses.update(s for s in sources if s.path == component)

        source.uses.discard(source)


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
            rf"((?:^[ \t]*///[^\n]*\n)+)(?:^[ \t]*\[[^\n]*\n)*^[^\n]*\b(?:class|struct|interface|enum|record)(?:\s+(?:class|struct))?\s+{re.escape(type_name)}\b",
            re.MULTILINE)
        match = pattern.search(source.text)
        if match and (summary := XML_SUMMARY.search(match.group(1))):
            return clean_xml_summary(summary.group(1))
    return None


def leading_comment_summary(source: SourceFile) -> str | None:
    text = source.text
    if source.path.suffix == ".razor":
        match = LEADING_RAZOR_COMMENT.match(text)
    elif source.path.suffix in (".js", ".css"):
        match = LEADING_LINE_COMMENTS.match(text) or LEADING_BLOCK_COMMENT.match(text)
    else:
        match = None
    if not match:
        return None
    body = re.sub(r"^\s*(?://|\*)\s?", "", match.group(1), flags=re.MULTILINE)
    return " ".join(body.split()) or None


def generated_summary(source: SourceFile) -> str:
    role, stem = source.role, source.stem
    words = humanize(stem)
    without_suffix = lambda suffix: humanize(stem[: -len(suffix)]) if stem.endswith(suffix) else words
    if role == "vendor":
        return f"Third-party library file `{source.name}`."
    if role == "page":
        route = RAZOR_PAGE_ROUTE.search(source.text).group(1)
        return f"Routable page at `{route}` for {without_suffix('Page')}."
    if role == "component":
        return f"UI component for {without_suffix('Component')}."
    if role == "code-behind":
        return f"Code-behind logic for the {stem} component."
    if role == "scoped-style":
        return f"Scoped styles for the {stem} component."
    if role == "stylesheet":
        return f"Global stylesheet for {words}."
    if role == "script":
        return f"Client-side script for {words}."
    if role == "interface":
        return f"Contract for the {humanize(stem[1:])}."
    if role == "service":
        return f"Service that handles {without_suffix('Service')}."
    if role == "repository":
        return f"Repository that persists and loads {without_suffix('Repository')}."
    if role == "helper":
        return f"Helper methods for {humanize(re.sub(r'Helpers?$', '', stem))}."
    if role == "extensions":
        return f"Extension methods for {without_suffix('Extensions')}."
    if role == "settings":
        return f"Configuration settings for {humanize(re.sub(r'(Options|Settings|Config)$', '', stem))}."
    if role == "enum":
        return f"Enumeration of {words} values."
    if role == "model":
        return f"Models, records and DTOs for {humanize(re.sub(r'(Models?|Dto|Record)$', '', stem))}."
    return f"Defines {words}."


def shorten(summary: str) -> str:
    if len(summary) <= SUMMARY_MAX_CHARS:
        return summary
    cut = summary[:SUMMARY_MAX_CHARS]
    sentence_end = cut.rfind(". ")
    return cut[: sentence_end + 1] if sentence_end > 80 else cut.rsplit(" ", 1)[0] + "…"


def implemented_interface_summary(source: SourceFile, owners: dict[str, list[SourceFile]]) -> str | None:
    base_list = re.search(rf"\b(?:class|record|struct)\s+{re.escape(source.stem)}\b[^{{;]*?:\s*([^{{;]+)", source.text)
    if not base_list:
        return None
    for interface in re.findall(r"\b(I[A-Z]\w*)", base_list.group(1)):
        for owner in owners.get(interface, []):
            if summary := xml_doc_summary(owner):
                return summary
    return None


def summarize(source: SourceFile, by_path: dict[Path, SourceFile], owners: dict[str, list[SourceFile]]) -> str:
    summary = (xml_doc_summary(source) or leading_comment_summary(source)
               or implemented_interface_summary(source, owners))
    if not summary and source.name.endswith(".razor"):
        code_behind = by_path.get(source.path.with_name(source.name + ".cs"))
        summary = code_behind and xml_doc_summary(code_behind)
    return shorten(summary or generated_summary(source))


def render_note(source: SourceFile, used_by_count: int, by_path: dict[Path, SourceFile],
                owners: dict[str, list[SourceFile]]) -> str:
    lines = [
        "---",
        f"tags: [{source.role}]",
        f"source: Poink.Sim/{source.relative.as_posix()}",
        "---",
        f"# {source.name}",
        "",
        summarize(source, by_path, owners),
        "",
        f"**Used by:** {used_by_count} file{'s' if used_by_count != 1 else ''} (see Backlinks)",
        "",
        "## Uses",
    ]
    uses = sorted(source.uses, key=lambda s: s.relative.as_posix().lower())
    lines += [f"- [[{used.note_link_path}|{used.name}]]" for used in uses] or ["- _nothing in Poink.Sim_"]
    return "\n".join(lines) + "\n"


def clear_vault() -> None:
    for child in VAULT_ROOT.iterdir():
        if child.name in VAULT_RESERVED:
            continue
        shutil.rmtree(child) if child.is_dir() else child.unlink()


def write_graph_settings() -> None:
    color_groups = ",\n".join(
        f'    {{"query": "tag:#{role}", "color": {{"a": 1, "rgb": {rgb}}}}}' for role, rgb in ROLE_COLORS.items())
    settings_dir = VAULT_ROOT / ".obsidian"
    settings_dir.mkdir(exist_ok=True)
    (settings_dir / "graph.json").write_text(
        "{\n"
        '  "showTags": false,\n  "showAttachments": false,\n  "hideUnresolved": true,\n'
        '  "showOrphans": true,\n  "showArrow": true,\n  "collapse-color-groups": false,\n'
        f'  "colorGroups": [\n{color_groups}\n  ]\n'
        "}\n",
        encoding="utf-8")


def main() -> None:
    sources = discover_sources()
    for source in sources:
        source.declared_types = declared_types(source)
        source.role = classify(source)
    link_sources(sources)

    used_by_counts = {source: 0 for source in sources}
    for source in sources:
        for used in source.uses:
            used_by_counts[used] += 1

    by_path = {source.path: source for source in sources}
    owners = build_type_owners(sources)
    VAULT_ROOT.mkdir(exist_ok=True)
    clear_vault()
    for source in sources:
        note_path = VAULT_ROOT / source.note_link_path
        note_path.parent.mkdir(parents=True, exist_ok=True)
        note_path.write_text(render_note(source, used_by_counts[source], by_path, owners), encoding="utf-8")

    graph_settings = VAULT_ROOT / ".obsidian" / "graph.json"
    if not graph_settings.exists():
        write_graph_settings()

    link_count = sum(len(source.uses) for source in sources)
    print(f"Wrote {len(sources)} notes with {link_count} links to {VAULT_ROOT}")


if __name__ == "__main__":
    main()
