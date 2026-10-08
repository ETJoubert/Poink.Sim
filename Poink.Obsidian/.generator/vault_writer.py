"""Writes the vault: one note per file, a dated classification report, and graph colour groups."""

import json
import shutil
from collections import Counter, defaultdict
from datetime import date

from classification import CANDIDATE_TITLES, KIND_LABELS, SMALL_DATA_LINES, type_shape
from linking import SHARED_FEATURE
from sources import REPORTS_DIR_NAME, VAULT_RESERVED, VAULT_ROOT, SourceFile

FEATURE_PALETTE = [0xE6194B, 0x3CB44B, 0xFFE119, 0x4363D8, 0xF58231, 0x911EB4, 0x46F0F0, 0xF032E6,
                   0xBCF60C, 0xFABEBE, 0x008080, 0xE6BEFF, 0x9A6324, 0xFFFAC8, 0x800000, 0xAAFFC3,
                   0x808000, 0xFFD8B1, 0x000075]
SHARED_COLOR = 0x808080


def tag(text: str) -> str:
    return text.lower().replace(" ", "-")


def render_note(source: SourceFile, summary: str, summary_origin: str) -> str:
    tags = [source.role, f"feature/{tag(source.feature)}", f"kind/{source.kind}"]
    tags += [f"candidate/{code}" for code in dict.fromkeys(c.code for c in source.candidates)]
    lines = [
        "---",
        f"tags: [{', '.join(tags)}]",
        f"role: {source.role}",
        f"feature: {source.feature}",
        f"feature_source: {source.feature_source}",
        f"kind: {source.kind}",
        f"code_lines: {source.code_lines}",
        f"types: {len(source.types)}",
        f"methods: {sum(t.counts.methods for t in source.types)}",
        f"fan_in: {len(source.used_by)}",
        f"fan_out: {len(source.uses)}",
        f"summary_source: {summary_origin}",
        f"candidates: [{', '.join(dict.fromkeys(c.code for c in source.candidates))}]",
        f"source: Poink.Sim/{source.relative.as_posix()}",
        "---",
        f"# {source.name}",
        "",
        summary,
        "",
        f"**Feature:** {source.feature}{' (by usage)' if source.feature_source == 'usage' else ''}"
        f" · **Kind:** {KIND_LABELS.get(source.kind, source.kind)}"
        f" · **Code lines:** {source.code_lines} · **Used by:** {len(source.used_by)}",
    ]
    if source.types:
        lines += ["", "## Types"]
        lines += [f"- `{t.name}` — {t.keyword}{', nested' if t.is_nested else ''}, {KIND_LABELS.get(t.kind, t.kind)}"
                  f" · {type_shape(t)}" for t in source.types]
    if source.candidates:
        lines += ["", "## Candidates"]
        lines += [f"- **{c.code}** — {c.action}" for c in source.candidates]
    lines += ["", "## Uses"]
    uses = sorted(source.uses, key=lambda s: s.relative.as_posix().lower())
    lines += [f"- {used.wikilink}" for used in uses] or ["- _nothing in Poink.Sim_"]
    return "\n".join(lines) + "\n"


def clear_vault() -> None:
    for child in VAULT_ROOT.iterdir():
        if child.name in VAULT_RESERVED:
            continue
        shutil.rmtree(child) if child.is_dir() else child.unlink()


def write_notes(sources: list[SourceFile], summaries: dict[SourceFile, tuple[str, str]]) -> None:
    VAULT_ROOT.mkdir(exist_ok=True)
    clear_vault()
    for source in sources:
        note_path = VAULT_ROOT / source.note_link_path
        note_path.parent.mkdir(parents=True, exist_ok=True)
        note_path.write_text(render_note(source, *summaries[source]), encoding="utf-8")


def features_by_size(sources: list[SourceFile]) -> list[tuple[str, int]]:
    counts = Counter(s.feature for s in sources if s.feature != SHARED_FEATURE)
    return counts.most_common()


def write_graph_settings(sources: list[SourceFile]) -> None:
    settings_path = VAULT_ROOT / ".obsidian" / "graph.json"
    settings_path.parent.mkdir(exist_ok=True)
    settings = json.loads(settings_path.read_text(encoding="utf-8")) if settings_path.exists() else {}
    groups = [{"query": f"tag:#feature/{tag(feature)}", "color": {"a": 1, "rgb": FEATURE_PALETTE[i % len(FEATURE_PALETTE)]}}
              for i, (feature, _) in enumerate(features_by_size(sources))]
    groups.append({"query": f"tag:#feature/{tag(SHARED_FEATURE)}", "color": {"a": 1, "rgb": SHARED_COLOR}})
    settings.update({"search": f"-path:{REPORTS_DIR_NAME}", "colorGroups": groups, "showArrow": True,
                     "hideUnresolved": True, "collapse-color-groups": False})
    settings_path.write_text(json.dumps(settings, indent=2) + "\n", encoding="utf-8")


def percent(part: int, whole: int) -> str:
    return f"{(100 * part / whole):.0f}%" if whole else "0%"


def render_report(sources: list[SourceFile], summaries: dict[SourceFile, tuple[str, str]], version: int, today: date) -> str:
    total = len(sources)
    extensions = Counter(s.path.suffix for s in sources)
    with_candidates = [s for s in sources if s.candidates]
    documented = sum(1 for s in sources if summaries[s][1] == "documented")
    all_types = [t for s in sources for t in s.types if t.keyword != "component"]

    lines = [
        "---", "tags: [report]", f"version: {version}", f"date: {today.isoformat()}", "---",
        f"# Code classification v{version} — {today.isoformat()}", "",
        f"Scope: {total} Poink.Sim files ({', '.join(f'{n} {ext}' for ext, n in extensions.most_common())}). "
        "Generated code is excluded: EF migrations, `*.Designer.cs`, model snapshots, minified and vendor libraries.",
        "", "## At a glance", "", "| Measure | Value |", "|---|---|",
        f"| Files | {total} |",
        f"| Links | {sum(len(s.uses) for s in sources)} |",
        f"| Declared C# types | {len(all_types)} |",
        f"| Files with their own summary doc | {documented} ({percent(documented, total)}) |",
        f"| Files with at least one candidate | {len(with_candidates)} ({percent(len(with_candidates), total)}) |",
        "", "## File kinds", "", "| Kind | Files | Share | Median code lines |", "|---|---|---|---|",
    ]
    by_kind: dict = defaultdict(list)
    for source in sources:
        by_kind[source.kind].append(source)
    for kind, files in sorted(by_kind.items(), key=lambda item: -len(item[1])):
        median = sorted(f.code_lines for f in files)[len(files) // 2]
        lines.append(f"| {KIND_LABELS.get(kind, kind)} | {len(files)} | {percent(len(files), total)} | {median} |")

    lines += ["", "## C# types by shape", "", "Counts every declared type, nested ones included.", "",
              "| Shape | Types | Share |", "|---|---|---|"]
    for kind, count in Counter(t.kind for t in all_types).most_common():
        lines.append(f"| {KIND_LABELS.get(kind, kind)} | {count} | {percent(count, len(all_types))} |")

    lines += ["", "## Features", "", "| Feature | Files | By folder | By usage | With candidates |", "|---|---|---|---|---|"]
    by_feature: dict = defaultdict(list)
    for source in sources:
        by_feature[source.feature].append(source)
    for feature, files in sorted(by_feature.items(), key=lambda item: -len(item[1])):
        lines.append(f"| {feature} | {len(files)} | {sum(f.feature_source == 'folder' for f in files)} | "
                     f"{sum(f.feature_source == 'usage' for f in files)} | {sum(bool(f.candidates) for f in files)} |")

    lines += ["", "## Candidates", "",
              "Heuristic findings to review, not verdicts. Each file's note repeats its own candidates."]
    for code, title in CANDIDATE_TITLES.items():
        rows = [(s, c) for s in sources for c in s.candidates if c.code == code]
        if not rows:
            continue
        lines += ["", f"### {title} ({len(rows)})", "", "| File | Folder | Feature | Lines | Used by | Suggestion |",
                  "|---|---|---|---|---|---|"]
        for source, candidate in sorted(rows, key=lambda r: (r[0].folder.as_posix().lower(), r[0].name.lower())):
            lines.append(f"| {source.wikilink} | {source.folder.as_posix()} | {source.feature} | {source.code_lines} | "
                         f"{len(source.used_by)} | {candidate.action.replace('|', '/')} |")

    lines += ["", "## Guideline observations", ""] + guideline_observations(sources, documented)
    lines += ["", "## Using this in Obsidian", "",
              "- Graph colours are by feature; grey is Shared. Reports are filtered out of the graph.",
              "- Graph filter `tag:#candidate` shows only files with findings; `tag:#candidate/merge-data` narrows to one kind.",
              "- Graph filter `tag:#feature/trade` isolates one feature; `-tag:#feature/shared` hides shared code.",
              "- Each note's properties (`kind`, `code_lines`, `fan_in`, `fan_out`, `candidates`) can be sorted in a Bases table."]
    return "\n".join(lines) + "\n"


def guideline_observations(sources: list[SourceFile], documented: int) -> list[str]:
    def files_with(code: str) -> list[SourceFile]:
        return [s for s in sources if any(c.code == code for c in s.candidates)]

    observations = []
    merge_data = files_with("merge-data")
    if merge_data:
        folders = {s.folder for s in merge_data}
        observations.append(
            f"- **One type per file for small data ({len(merge_data)} files, {len(folders)} folders).** "
            "CLAUDE.md §2.3 names `*ThingModels` as the home for models, DTOs and records but never says when to merge. "
            f"A rule such as *“records, enums and DTOs under {SMALL_DATA_LINES} lines live in the feature's `<Feature>Models.cs`”* "
            "would stop new one-record files.")
    single = files_with("single-method")
    if single:
        single_user = sum(1 for s in single if len(s.used_by) == 1)
        observations.append(
            f"- **Single-method classes ({len(single)}, {single_user} with exactly one user).** "
            "SOLID is being read as “one method per class”. A rule such as *“a class with one method and one caller "
            "is a private method on that caller unless it is injected or reused”* keeps SRP without the file count.")
    services = files_with("single-method-service")
    if services:
        observations.append(
            f"- **One-operation services behind interfaces ({len(services)}).** The DI rule in §2.1 is met, "
            "but each operation gets its own interface, implementation and registration. Consider grouping related operations per feature service.")
    sprawl = files_with("helper-sprawl")
    if sprawl:
        observations.append(
            f"- **Helper sprawl ({len(sprawl)} files in {len({s.folder for s in sprawl})} folders).** "
            "§2.2 asks for helpers grouped by purpose; a rule like *“one `ThingHelper` per purpose per feature, "
            "extend it before adding a new one”* makes that concrete.")
    moved = files_with("move-to-feature")
    if moved:
        observations.append(
            f"- **Shared/core code used by one feature ({len(moved)}).** A rule such as *“only code used by two or more "
            "features goes into Shared, Models, Helpers or Repository”* keeps features self-contained.")
    unused = files_with("unused")
    if unused:
        observations.append(
            f"- **Unreferenced files ({len(unused)}).** Check before deleting: DI by name, reflection or JS interop can hide usage.")
    undocumented = len(sources) - documented
    observations.append(
        f"- **Summaries ({undocumented} files, {percent(undocumented, len(sources))}, have none of their own).** "
        "§4 already permits XML docs on public members; requiring a `<summary>` on each top-level type "
        "improves this map and gives the model a cheaper way to navigate than reading whole files.")
    return observations


def write_report(sources: list[SourceFile], summaries: dict[SourceFile, tuple[str, str]], version: int) -> str:
    today = date.today()
    reports_dir = VAULT_ROOT / REPORTS_DIR_NAME
    reports_dir.mkdir(exist_ok=True)
    report_path = reports_dir / f"Code Classification v{version} {today.isoformat()}.md"
    report_path.write_text(render_report(sources, summaries, version, today), encoding="utf-8")
    return report_path.name
