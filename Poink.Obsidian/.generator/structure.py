"""Lightweight C# structure reader: finds type declarations and counts their members by kind.

It works on text with comments and string literals blanked, so braces and semicolons are structural.
"""

import re

from sources import MemberCounts, TypeInfo

TYPE_HEADER = re.compile(
    r"(?P<mods>(?:\b(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|file|unsafe|new)\s+)*)"
    r"\b(?P<keyword>record\s+struct|record\s+class|record|class|struct|interface|enum)\s+(?P<name>[A-Z]\w*)")
NESTED_TYPE_IN_HEADER = re.compile(r"\b(?:class|struct|interface|enum|record|delegate)\s+[A-Z]\w*")
LEADING_ATTRIBUTES = re.compile(r"^\s*(?:\[[^\[\]]*(?:\[[^\[\]]*\][^\[\]]*)*\]\s*)+")
CALLABLE_NAME = re.compile(r"(~?[A-Za-z_]\w*)\s*(?:<[^()]*>)?\s*\($")
GENERIC_CONSTRAINTS = re.compile(r"\s+where\s+\w+\s*:.*$")
CONSTRUCTOR_CHAIN = re.compile(r"\)\s*:\s*(?:base|this)\s*\(.*$")
RAZOR_CODE_BLOCK =re.compile(r"@(?:code|functions)\s*\{")

OPENERS = {"(": ")", "[": "]", "{": "}"}


def matching_close(text: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index
    return len(text) - 1


def matching_paren(text: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        if text[index] in "([":
            depth += 1
        elif text[index] in ")]":
            depth -= 1
            if depth == 0:
                return index
    return len(text) - 1


def without_bracketed(text: str) -> str:
    result, depth = [], 0
    for char in text:
        if char in "([":
            depth += 1
        elif char in ")]":
            depth -= 1
        elif depth == 0:
            result.append(char)
    return "".join(result)


def split_top_level(text: str, separator: str) -> list[str]:
    parts, depth, start = [], 0, 0
    for index, char in enumerate(text):
        if char in "([{<":
            depth += 1
        elif char in ")]}>":
            depth -= 1
        elif char == separator and depth == 0:
            parts.append(text[start:index])
            start = index + 1
    parts.append(text[start:])
    return [part for part in parts if part.strip()]


def split_members(body: str) -> list[tuple[str, bool]]:
    members, start, index, paren_depth = [], 0, 0, 0
    while index < len(body):
        char = body[index]
        if char in "([":
            paren_depth += 1
        elif char in ")]":
            paren_depth -= 1
        elif char == ";" and paren_depth == 0:
            members.append((body[start:index], False))
            start = index + 1
        elif char == "{" and paren_depth == 0:
            close = matching_close(body, index)
            header = body[start:index]
            if "=" in without_bracketed(header):
                index = close + 1
                continue
            members.append((header, True))
            index = close + 1
            next_char = body[index:].lstrip()[:1]
            if next_char == "=":
                semicolon = body.find(";", index)
                index = semicolon + 1 if semicolon >= 0 else len(body)
            start = index
            continue
        index += 1
    return members


def classify_member(header: str, type_name: str, has_block: bool) -> tuple[str, str] | None:
    text = " ".join(LEADING_ATTRIBUTES.sub("", header).split())
    if not text:
        return None
    if NESTED_TYPE_IN_HEADER.search(without_bracketed(text)):
        return ("nested", "")
    if re.search(r"\bevent\b", text):
        return ("event", "")
    if re.search(r"\boperator\b", text):
        return ("operator", "")
    if re.search(r"\boverride\b", text) and "(" in text:
        return ("override", "")

    signature = text
    flat = without_bracketed(text)
    if "=" in flat:
        cut = len(flat.split("=", 1)[0])
        signature = _prefix_matching_flat(text, cut)
    signature = CONSTRUCTOR_CHAIN.sub(")", GENERIC_CONSTRAINTS.sub("", signature))
    if re.search(r"\bthis\s*\[", signature):
        return ("property", "")
    if signature.rstrip().endswith(")"):
        open_index = _matching_open_paren(signature)
        callable_match = CALLABLE_NAME.search(signature[: open_index + 1])
        if callable_match:
            name = callable_match.group(1)
            if name.lstrip("~") == type_name:
                return ("constructor", name)
            return ("method", name)
    if has_block or "=>" in text:
        return ("property", "")
    return ("field", "")


def _prefix_matching_flat(text: str, flat_length: int) -> str:
    depth, kept = 0, 0
    for index, char in enumerate(text):
        if char in "([":
            depth += 1
        elif char in ")]":
            depth -= 1
        elif depth == 0:
            if kept == flat_length:
                return text[:index]
            kept += 1
    return text


def _matching_open_paren(signature: str) -> int:
    depth = 0
    for index in range(len(signature) - 1, -1, -1):
        if signature[index] == ")":
            depth += 1
        elif signature[index] == "(":
            depth -= 1
            if depth == 0:
                return index
    return 0


def count_members(body: str, type_name: str) -> tuple[MemberCounts, list[str]]:
    counts, method_names = MemberCounts(), []
    for header, has_block in split_members(body):
        classified = classify_member(header, type_name, has_block)
        if not classified:
            continue
        member_kind, name = classified
        if member_kind == "method":
            counts.methods += 1
            method_names.append(name)
        elif member_kind == "constructor":
            counts.constructors += 1
        elif member_kind == "property":
            counts.properties += 1
        elif member_kind == "field":
            counts.fields += 1
        elif member_kind == "event":
            counts.events += 1
    return counts, method_names


def parse_csharp_types(structure_text: str) -> list[TypeInfo]:
    found = []
    for match in TYPE_HEADER.finditer(structure_text):
        name, keyword = match.group("name"), " ".join(match.group("keyword").split())
        modifiers = match.group("mods") or ""
        cursor = match.end()
        generic = re.match(r"\s*<[^(){};]*>", structure_text[cursor:])
        if generic:
            cursor += generic.end()

        counts = MemberCounts()
        positional = re.match(r"\s*\(", structure_text[cursor:])
        if positional and keyword.startswith("record"):
            open_index = cursor + positional.end() - 1
            close_index = matching_paren(structure_text, open_index)
            counts.properties += len(split_top_level(structure_text[open_index + 1:close_index], ","))
            cursor = close_index + 1

        body_start = _next_top_level(structure_text, cursor, "{;")
        header_tail = structure_text[cursor:body_start]
        base_list = header_tail.split(":", 1)[1].split(" where ")[0].strip() if ":" in header_tail else ""
        if body_start >= len(structure_text) or structure_text[body_start] == ";":
            found.append((match.start(), match.start(), TypeInfo(name, keyword, "static" in modifiers, False,
                                                                 base_list, counts, [])))
            continue

        body_end = matching_close(structure_text, body_start)
        body = _blank_nested_bodies(structure_text[body_start + 1:body_end])
        if keyword == "enum":
            counts.enum_values = len(split_top_level(body, ","))
            method_names = []
        else:
            member_counts, method_names = count_members(body, name)
            counts.methods += member_counts.methods
            counts.properties += member_counts.properties
            counts.fields += member_counts.fields
            counts.constructors += member_counts.constructors
            counts.events += member_counts.events
        found.append((body_start, body_end, TypeInfo(name, keyword, "static" in modifiers, False,
                                                     base_list, counts, method_names)))

    for start, _, type_info in found:
        type_info.is_nested = any(outer_start < start < outer_end
                                  for outer_start, outer_end, outer in found if outer is not type_info)
    return [type_info for _, _, type_info in found]


def parse_razor_component(structure_text: str, component_name: str) -> TypeInfo:
    counts, method_names = MemberCounts(), []
    for match in RAZOR_CODE_BLOCK.finditer(structure_text):
        open_index = match.end() - 1
        body = structure_text[open_index + 1:matching_close(structure_text, open_index)]
        block_counts, block_methods = count_members(_blank_nested_bodies(body), component_name)
        counts.methods += block_counts.methods
        counts.properties += block_counts.properties
        counts.fields += block_counts.fields
        counts.constructors += block_counts.constructors
        method_names += block_methods
    return TypeInfo(component_name, "component", False, False, "", counts, method_names)


def _next_top_level(text: str, start: int, stops: str) -> int:
    depth = 0
    for index in range(start, len(text)):
        char = text[index]
        if char in "([<":
            depth += 1
        elif char in ")]>":
            depth = max(depth - 1, 0)
        elif char in stops and depth == 0:
            return index
    return len(text)


def _blank_nested_bodies(body: str) -> str:
    result, index = [], 0
    for match in TYPE_HEADER.finditer(body):
        if match.start() < index:
            continue
        open_index = _next_top_level(body, match.end(), "{;")
        if open_index >= len(body) or body[open_index] == ";":
            continue
        close_index = matching_close(body, open_index)
        result.append(body[index:open_index])
        result.append(";")
        index = close_index + 1
    result.append(body[index:])
    return "".join(result)
