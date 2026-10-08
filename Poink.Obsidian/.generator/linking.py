"""Resolves which files use which, then assigns each file to a feature by folder or by who uses it."""

import re

from sources import SourceFile, strip_comments, strip_for_links, strip_for_structure

TYPE_DECLARATION = re.compile(
    r"\b(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?|delegate\s+[\w<>\[\],.? ]+?)\s+([A-Z]\w*)")
TYPE_REFERENCE = re.compile(r"(?<![\w.])([A-Z]\w*)")
EXTENSION_METHOD_DECLARATION = re.compile(r"\bstatic\s+[^=;{}()]+?\s+(\w+)\s*(?:<[^>()]*>)?\s*\(\s*this\s")
MEMBER_CALL = re.compile(r"\.([A-Z]\w*)\s*(?:<[^>()]*>)?\s*\(")
QUALIFIED_REFERENCE = re.compile(r"(?=\b([A-Z]\w*)\.([A-Z]\w*))")
NAMESPACE_DECLARATION = re.compile(r"^\s*@?namespace\s+([\w.]+)", re.MULTILINE)
REFLECTION_SCAN = re.compile(r"typeof\((I[A-Z]\w*)\)\.IsAssignableFrom")

FEATURE_FOLDERS = {("Components", "Pages"), ("Components", "Charts")}
FEATURE_SELF_FOLDERS = {("Components", "Layout"), ("Components", "RuleEngine")}
SHARED_FEATURE = "Shared"
INFERENCE_PASSES = 5


def declared_types(source: SourceFile) -> list[str]:
    if source.is_razor:
        return [source.stem]
    if source.path.suffix != ".cs":
        return []
    return list(dict.fromkeys(TYPE_DECLARATION.findall(strip_for_structure(source.text))))


def build_type_owners(sources: list[SourceFile]) -> dict[str, list[SourceFile]]:
    declarers: dict[str, list[SourceFile]] = {}
    for source in sources:
        for type_name in source.declared_types:
            declarers.setdefault(type_name, []).append(source)
    return {
        type_name: ([f for f in files if f.name == f"{type_name}.razor"]
                    or [f for f in files if f.name == f"{type_name}.cs"]
                    or files)
        for type_name, files in declarers.items()
    }


def build_extension_method_owners(sources: list[SourceFile]) -> dict[str, list[SourceFile]]:
    owners: dict[str, list[SourceFile]] = {}
    for source in sources:
        if source.path.suffix == ".cs":
            for method_name in set(EXTENSION_METHOD_DECLARATION.findall(strip_for_structure(source.text))):
                owners.setdefault(method_name, []).append(source)
    return owners


def namespace_segments(sources: list[SourceFile], owners: dict[str, list[SourceFile]]) -> set[str]:
    segments = {segment for source in sources if source.is_code
                for namespace in NAMESPACE_DECLARATION.findall(source.text) for segment in namespace.split(".")}
    return segments - owners.keys()


def build_implementers(sources: list[SourceFile]) -> dict[str, list[SourceFile]]:
    implementers: dict[str, list[SourceFile]] = {}
    for source in sources:
        for type_info in source.types:
            for base in type_info.base_list.split(","):
                base_name = base.strip().split("<")[0].split("(")[0].strip()
                if re.fullmatch(r"I[A-Z]\w*", base_name) and source not in implementers.get(base_name, []):
                    implementers.setdefault(base_name, []).append(source)
    return implementers


def referenced_type_names(code: str, namespaces: set[str]) -> set[str]:
    names = set(TYPE_REFERENCE.findall(code))
    names.update(member for qualifier, member in QUALIFIED_REFERENCE.findall(code) if qualifier in namespaces)
    return names


def link_sources(sources: list[SourceFile]) -> None:
    owners = build_type_owners(sources)
    extension_owners = build_extension_method_owners(sources)
    implementers = build_implementers(sources)
    namespaces = namespace_segments(sources, owners)
    assets = [s for s in sources if s.is_asset]
    by_path = {s.path: s for s in sources}

    for source in sources:
        if source.is_code:
            code = strip_for_links(source.text, source.is_razor)
            for type_name in referenced_type_names(code, namespaces) - set(source.declared_types):
                source.uses.update(owners.get(type_name, []))
            for interface in REFLECTION_SCAN.findall(code):
                source.uses.update(implementers.get(interface, []))
            for method_name in set(MEMBER_CALL.findall(code)):
                source.uses.update(extension_owners.get(method_name, []))
            if source.stem in source.declared_types:
                source.uses.update(owners.get(source.stem, []))

        uncommented = strip_comments(source.text)
        for asset in assets:
            if re.search(rf"(?<![\w.-]){re.escape(asset.name)}\b", uncommented):
                source.uses.add(asset)

        if source.name.endswith((".razor.cs", ".razor.css")):
            component = by_path.get(source.path.with_name(f"{source.stem}.razor"))
            if component:
                source.uses.add(component)

        source.uses.discard(source)

    for source in sources:
        for used in source.uses:
            used.used_by.add(source)


def folder_feature(source: SourceFile) -> str | None:
    parts = source.relative.parts
    if len(parts) > 3 and tuple(parts[:2]) in FEATURE_FOLDERS:
        return parts[2]
    if len(parts) > 2 and tuple(parts[:2]) in FEATURE_SELF_FOLDERS:
        return parts[1]
    if len(parts) > 2 and parts[0] == "Services":
        return parts[1]
    if len(parts) > 2 and parts[0] == "Models" and parts[1].endswith("Models"):
        return parts[1].removesuffix("Models")
    return None


def is_composition_root(source: SourceFile) -> bool:
    return source.name == "Program.cs" or source.relative.parts[:2] == ("Services", "Registration")


def effective_users(source: SourceFile, owners: dict[str, list[SourceFile]]) -> set[SourceFile]:
    """Callers that depend on the file directly or through an interface it implements, excluding DI registration."""
    users = set(source.used_by)
    for type_info in source.types:
        for base in type_info.base_list.split(","):
            base_name = base.strip().split("<")[0].strip()
            if re.fullmatch(r"I[A-Z]\w*", base_name):
                for interface_file in owners.get(base_name, []):
                    users.update(interface_file.used_by)
    return {user for user in users if user is not source and not is_composition_root(user)}


def assign_features(sources: list[SourceFile]) -> None:
    for source in sources:
        feature = folder_feature(source)
        if feature:
            source.feature, source.feature_source = feature, "folder"

    owners = build_type_owners(sources)
    users_by_source = {source: effective_users(source, owners) for source in sources}
    for _ in range(INFERENCE_PASSES):
        changed = False
        for source in sources:
            if source.feature_source == "folder":
                continue
            user_features = {user.feature for user in users_by_source[source]}
            inferred = next(iter(user_features)) if len(user_features) == 1 else None
            if inferred and inferred != SHARED_FEATURE and source.feature != inferred:
                source.feature, source.feature_source = inferred, "usage"
                changed = True
        if not changed:
            break
