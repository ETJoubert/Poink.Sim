"""Source discovery, the shared file model, and text cleaning used by every other stage."""

import re
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / "Poink.Sim"
VAULT_ROOT = REPO_ROOT / "Poink.Obsidian"
REPORTS_DIR_NAME = "_Reports"
VAULT_RESERVED = {".obsidian", ".generator", REPORTS_DIR_NAME}

SOURCE_EXTENSIONS = (".cs", ".razor", ".css", ".js", ".html")
EXCLUDED_DIRS = {"bin", "obj", "logs", "Properties", "Migrations", "lib", "bootstrap"}
GENERATED_FILE = re.compile(r"(\.Designer\.cs|\.g\.cs|\.g\.i\.cs|ModelSnapshot\.cs|\.min\.css|\.min\.js)$")

LINK_NOISE = re.compile(
    r"//[^\n]*|/\*.*?\*/|@\*.*?\*@|\"(?:\\.|[^\"\\\n])*\"|'(?:\\.|[^'\\\n])'", re.DOTALL)
COMMENTS_ONLY = re.compile(
    r"\"(?:\\.|[^\"\\\n])*\"|(?<![:\w])//[^\n]*|/\*.*?\*/|@\*.*?\*@|<!--.*?-->", re.DOTALL)
STRUCTURE_NOISE = re.compile(
    r"\"\"\".*?\"\"\"|@\"(?:[^\"]|\"\")*\"|(?<![:\w])//[^\n]*|/\*.*?\*/|@\*.*?\*@|<!--.*?-->"
    r"|\"(?:\\.|[^\"\\\n])*\"|'(?:\\.|[^'\\\n])'", re.DOTALL)
MARKUP_TEXT_NODE = re.compile(r">([^<>@{}]*)<")


@dataclass
class MemberCounts:
    methods: int = 0
    properties: int = 0
    fields: int = 0
    constructors: int = 0
    events: int = 0
    enum_values: int = 0


@dataclass
class TypeInfo:
    name: str
    keyword: str
    is_static: bool
    is_nested: bool
    base_list: str
    counts: MemberCounts
    method_names: list[str]
    kind: str = ""


@dataclass
class Candidate:
    code: str
    action: str
    related: list["SourceFile"] = field(default_factory=list)


@dataclass(eq=False)
class SourceFile:
    path: Path
    text: str
    role: str = "class"
    declared_types: list[str] = field(default_factory=list)
    types: list[TypeInfo] = field(default_factory=list)
    uses: set["SourceFile"] = field(default_factory=set)
    used_by: set["SourceFile"] = field(default_factory=set)
    feature: str = "Shared"
    feature_source: str = "shared"
    kind: str = ""
    code_lines: int = 0
    candidates: list[Candidate] = field(default_factory=list)

    @property
    def relative(self) -> Path:
        return self.path.relative_to(SOURCE_ROOT)

    @property
    def folder(self) -> Path:
        return self.relative.parent

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
    def wikilink(self) -> str:
        return f"[[{self.note_link_path}|{self.name}]]"

    @property
    def is_code(self) -> bool:
        return self.path.suffix in (".cs", ".razor")

    @property
    def is_razor(self) -> bool:
        return self.path.suffix == ".razor"

    @property
    def is_asset(self) -> bool:
        return self.path.suffix in (".css", ".js", ".html") and not self.name.endswith(".razor.css")

    @property
    def primary_type(self) -> TypeInfo | None:
        top_level = [t for t in self.types if not t.is_nested]
        named = [t for t in top_level if t.name == self.stem]
        return (named or top_level or [None])[0]


def discover_sources() -> list[SourceFile]:
    sources = []
    for path in sorted(SOURCE_ROOT.rglob("*")):
        relative_parts = path.relative_to(SOURCE_ROOT).parts
        if not path.is_file() or path.suffix not in SOURCE_EXTENSIONS or GENERATED_FILE.search(path.name):
            continue
        if any(part in EXCLUDED_DIRS for part in relative_parts[:-1]):
            continue
        sources.append(SourceFile(path, path.read_text(encoding="utf-8-sig", errors="replace")))
    return sources


def strip_for_links(text: str, is_markup: bool) -> str:
    def keep_identifier_like_strings(match: re.Match) -> str:
        token = match.group(0)
        if is_markup and token.startswith('"') and not re.search(r"\s", token):
            return token
        return " "

    cleaned = LINK_NOISE.sub(keep_identifier_like_strings, text)
    return MARKUP_TEXT_NODE.sub("><", cleaned) if is_markup else cleaned


def strip_comments(text: str) -> str:
    return COMMENTS_ONLY.sub(lambda m: m.group(0) if m.group(0).startswith('"') else " ", text)


def strip_for_structure(text: str) -> str:
    def blank_preserving_lines(match: re.Match) -> str:
        token = match.group(0)
        if token.startswith(('"', "@\"", "'")):
            return '""' + "\n" * token.count("\n")
        return "\n" * token.count("\n")

    return STRUCTURE_NOISE.sub(blank_preserving_lines, text)


def count_code_lines(structure_text: str) -> int:
    return sum(1 for line in structure_text.splitlines() if line.strip() and line.strip() not in ("{", "}", "};"))
