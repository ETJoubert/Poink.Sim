"""Classifies each type and file by shape, then proposes consolidation candidates."""

import re
from collections import Counter, defaultdict

from linking import SHARED_FEATURE, folder_feature, is_composition_root
from sources import Candidate, SourceFile, TypeInfo, count_code_lines, strip_for_structure
from structure import parse_csharp_types, parse_razor_component

SMALL_DATA_LINES = 40
TINY_FILE_LINES = 15
HELPER_SPRAWL_MIN_FILES = 3
HELPER_SPRAWL_MAX_METHODS = 2
DATA_WITH_HELPER_MIN_PROPERTIES = 3
ENTRY_POINTS = {"App.razor", "Routes.razor", "_Imports.razor", "Program.cs"}
DATA_KINDS = {"data", "enum", "data-bundle"}
ONE_PER_FILE_FOLDERS = {("Data", "Entities")}
GUIDELINE_LAYER_FOLDERS = {"Repository", "Data"}

KIND_LABELS = {
    "data": "data type (at most one helper method)",
    "enum": "enum",
    "constants": "static constants",
    "data-bundle": "bundle of data types",
    "interface": "interface",
    "single-method": "single-method class",
    "static-helper": "static helper",
    "behavior": "class with behaviour",
    "mixed": "mixed types",
    "component": "Razor component",
    "page": "Razor page",
    "component-code": "component code-behind",
    "scoped-style": "scoped stylesheet",
    "stylesheet": "stylesheet",
    "script": "script",
    "no-types": "no type declarations",
}

CANDIDATE_TITLES = {
    "merge-data": "Small data files that could share one *Models file",
    "single-method": "Single-method classes",
    "single-method-service": "Single-method services behind an interface",
    "tiny-single-user": "Tiny files with exactly one user",
    "helper-sprawl": "Folders with many small helpers",
    "move-to-feature": "Shared/core files used by only one feature",
    "unused": "Files nothing references",
}


RAZOR_PAGE_ROUTE = re.compile(r"^@page\s+\"([^\"]+)\"", re.MULTILINE)
ROLE_SUFFIXES = (("Service", "service"), ("Repository", "repository"), ("Helper", "helper"),
                 ("Helpers", "helper"), ("Extensions", "extensions"), ("Options", "settings"),
                 ("Settings", "settings"), ("Config", "settings"), ("Models", "model"),
                 ("Model", "model"), ("Dto", "model"), ("Record", "model"))


def classify_role(source: SourceFile) -> str:
    name, stem = source.name, source.stem
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
    for suffix, role in ROLE_SUFFIXES:
        if stem.endswith(suffix):
            return role
    if source.declared_types and all(re.search(rf"\benum\s+{t}\b", source.text) for t in source.declared_types):
        return "enum"
    if "Models" in source.relative.parts:
        return "model"
    return "class"


def analyse_structure(source: SourceFile) -> None:
    structure_text = strip_for_structure(source.text)
    source.code_lines = count_code_lines(structure_text)
    if source.path.suffix == ".cs":
        source.types = parse_csharp_types(structure_text)
    elif source.is_razor:
        source.types = [parse_razor_component(structure_text, source.stem)]
    for type_info in source.types:
        type_info.kind = classify_type(type_info, source)
    source.kind = classify_file(source)


def classify_type(type_info: TypeInfo, source: SourceFile) -> str:
    methods = type_info.counts.methods
    if type_info.keyword in ("enum", "interface", "component"):
        return type_info.keyword
    if source.name.endswith(".razor.cs") and type_info.name == source.stem:
        return "component"
    if methods == 0 and type_info.is_static:
        return "constants"
    if methods == 0 or (methods == 1 and not type_info.is_static and not type_info.base_list
                        and type_info.counts.properties >= DATA_WITH_HELPER_MIN_PROPERTIES):
        return "data"
    if type_info.is_static:
        return "static-helper" if methods > 1 else "single-method"
    return "single-method" if methods == 1 else "behavior"


def classify_file(source: SourceFile) -> str:
    if source.is_razor:
        return "page" if source.role == "page" else "component"
    if source.name.endswith(".razor.css"):
        return "scoped-style"
    if source.is_asset:
        return "stylesheet" if source.path.suffix == ".css" else "script"
    if source.name.endswith(".razor.cs"):
        return "component-code"
    top_level = [t for t in source.types if not t.is_nested]
    if not top_level:
        return "no-types"
    if len(top_level) == 1:
        return top_level[0].kind
    kinds = {t.kind for t in top_level}
    return "data-bundle" if kinds <= {"data", "enum"} else "mixed"


def type_shape(type_info: TypeInfo) -> str:
    counts = type_info.counts
    if type_info.keyword == "enum":
        return f"{counts.enum_values} values"
    parts = [(counts.methods, "method"), (counts.properties, "property"), (counts.fields, "field"),
             (counts.constructors, "constructor"), (counts.events, "event")]
    described = [f"{n} {label if n == 1 else ('properties' if label == 'property' else label + 's')}"
                 for n, label in parts if n]
    return ", ".join(described) or "empty"


def has_class_base(type_info: TypeInfo) -> bool:
    first_base = type_info.base_list.split(",")[0].strip()
    return bool(first_base) and not re.match(r"I[A-Z]", first_base)


def implemented_interfaces(type_info: TypeInfo) -> list[str]:
    return [b.strip().split("<")[0] for b in type_info.base_list.split(",") if re.match(r"\s*I[A-Z]", b)]


def names(files: list[SourceFile]) -> str:
    return ", ".join(f"`{f.name}`" for f in sorted(files, key=lambda f: f.name.lower()))


def reclassify_partial_parts(sources: list[SourceFile]) -> set[SourceFile]:
    component_names = {s.stem for s in sources if s.is_razor}
    declaring_files = Counter(name for s in sources for name in set(s.declared_types))
    partial_parts = set()
    for source in sources:
        primary = source.primary_type
        if source.path.suffix != ".cs" or not primary or declaring_files[primary.name] < 2:
            continue
        if source.name != f"{primary.name}.cs":
            partial_parts.add(source)
        if primary.name in component_names:
            source.kind = "component-code"
    return partial_parts


def find_candidates(sources: list[SourceFile], implementers: dict[str, list[SourceFile]]) -> None:
    partial_parts = reclassify_partial_parts(sources)
    by_folder: dict = defaultdict(list)
    for source in sources:
        by_folder[source.folder].append(source)

    for folder, files in by_folder.items():
        _merge_data_candidates(folder, files)
        _helper_sprawl_candidates(folder, files)

    for source in sources:
        _single_method_candidate(source, by_folder[source.folder], implementers)
        _tiny_single_user_candidate(source)
        _move_to_feature_candidate(source)
        if source not in partial_parts:
            _unused_candidate(source)


def _merge_data_candidates(folder, files: list[SourceFile]) -> None:
    if tuple(folder.parts[:2]) in ONE_PER_FILE_FOLDERS:
        return
    small_data = [f for f in files if f.kind in DATA_KINDS and f.code_lines <= SMALL_DATA_LINES
                  and not f.stem.endswith("Models")]
    existing_models = [f for f in files if f.stem.endswith("Models") and f.path.suffix == ".cs"]

    by_target: dict = defaultdict(list)
    for source in small_data:
        by_target[_merge_target(source, folder, existing_models)].append(source)

    for (target_name, target_exists), group in by_target.items():
        if len(group) < 2 and not target_exists:
            continue
        target_text = f"`{target_name}`" if target_exists else f"a new `{target_name}`"
        for source in group:
            siblings = [f for f in group if f is not source]
            action = f"Merge into {target_text}" + (f" with {names(siblings)}" if siblings else "")
            source.candidates.append(Candidate("merge-data", action + ".", siblings))


def _merge_target(source: SourceFile, folder, existing_models: list[SourceFile]) -> tuple[str, bool]:
    owner = source.feature if source.feature != SHARED_FEATURE else SHARED_FEATURE
    matching = [f for f in existing_models if re.match(rf"{re.escape(owner)}(?![a-z])", f.stem)]
    if matching:
        return max(matching, key=lambda f: f.code_lines).name, True
    if len(existing_models) == 1 and folder_feature(existing_models[0]) == folder_feature(source):
        return existing_models[0].name, True
    return f"{owner}Models.cs", False


def _helper_sprawl_candidates(folder, files: list[SourceFile]) -> None:
    small_helpers = [f for f in files if f.path.suffix == ".cs"
                     and (f.role == "helper" or f.kind in ("static-helper", "single-method"))
                     and f.primary_type and f.primary_type.is_static
                     and f.primary_type.counts.methods <= HELPER_SPRAWL_MAX_METHODS]
    if len(small_helpers) < HELPER_SPRAWL_MIN_FILES:
        return
    for source in small_helpers:
        siblings = [f for f in small_helpers if f is not source]
        source.candidates.append(Candidate(
            "helper-sprawl",
            f"One of {len(small_helpers)} small static helpers in `{folder.as_posix()}`; "
            f"consolidate by purpose with {names(siblings)}.", siblings))


def _single_method_candidate(source: SourceFile, folder_files: list[SourceFile],
                             implementers: dict[str, list[SourceFile]]) -> None:
    primary = source.primary_type
    if source.kind != "single-method" or not primary or has_class_base(primary):
        return
    method = primary.method_names[0] if primary.method_names else "its method"
    interfaces = implemented_interfaces(primary)
    users = sorted(source.used_by, key=lambda f: f.name)
    if users and all(is_composition_root(user) for user in users):
        return
    if interfaces:
        if all(len(implementers.get(i, [])) == 1 for i in interfaces):
            source.candidates.append(Candidate(
                "single-method-service",
                f"Only `{method}` behind `{interfaces[0]}`, its sole implementation; "
                "fold into a broader service unless it is a deliberate seam."))
        return
    if len(users) == 1:
        action = f"Inline `{method}` into its only user `{users[0].name}`."
    else:
        sibling_helpers = [f for f in folder_files if f is not source and f.primary_type
                           and f.primary_type.is_static and f.kind in ("static-helper", "single-method")]
        action = (f"Fold `{method}` into a sibling helper ({names(sibling_helpers)})." if sibling_helpers and primary.is_static
                  else f"`{method}` is used by {len(users)} files; check it earns its own type.")
    source.candidates.append(Candidate("single-method", action, users))


def _tiny_single_user_candidate(source: SourceFile) -> None:
    primary = source.primary_type
    if (source.kind in ("behavior", "static-helper", "mixed") and source.code_lines <= TINY_FILE_LINES
            and len(source.used_by) == 1 and not (primary and implemented_interfaces(primary))):
        user = next(iter(source.used_by))
        source.candidates.append(Candidate(
            "tiny-single-user", f"{source.code_lines} code lines used only by `{user.name}`; merge into it.", [user]))


def _move_to_feature_candidate(source: SourceFile) -> None:
    if source.feature_source != "usage" or folder_feature(source) or source.is_asset:
        return
    if source.relative.parts[0] in GUIDELINE_LAYER_FOLDERS:
        return
    if source.name.endswith((".razor.cs", ".razor.css")):
        return
    source.candidates.append(Candidate(
        "move-to-feature", f"Used only by the {source.feature} feature; move it into that feature's folder."))


def _unused_candidate(source: SourceFile) -> None:
    if source.used_by or source.kind in ("page", "component-code", "scoped-style") or source.name in ENTRY_POINTS:
        return
    source.candidates.append(Candidate("unused", "Nothing in Poink.Sim references it; confirm it is still needed."))
