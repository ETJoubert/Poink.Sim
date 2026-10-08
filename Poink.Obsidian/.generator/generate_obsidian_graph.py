"""Regenerates the Poink.Obsidian vault: one note per Poink.Sim source file, linked, classified and grouped by feature,
plus a dated classification report in _Reports.

Run from anywhere: python Poink.Obsidian/.generator/generate_obsidian_graph.py
"""

from classification import analyse_structure, classify_role, find_candidates
from linking import assign_features, build_implementers, build_type_owners, declared_types, link_sources
from sources import VAULT_ROOT, discover_sources
from summaries import summarize
from vault_writer import write_graph_settings, write_notes, write_report

VERSION = 2


def main() -> None:
    sources = discover_sources()
    for source in sources:
        source.declared_types = declared_types(source)
        source.role = classify_role(source)
        analyse_structure(source)

    link_sources(sources)
    assign_features(sources)
    find_candidates(sources, build_implementers(sources))

    by_path = {source.path: source for source in sources}
    owners = build_type_owners(sources)
    summaries = {source: summarize(source, by_path, owners) for source in sources}

    write_notes(sources, summaries)
    write_graph_settings(sources)
    report_name = write_report(sources, summaries, VERSION)

    link_count = sum(len(source.uses) for source in sources)
    candidate_files = sum(1 for source in sources if source.candidates)
    print(f"Wrote {len(sources)} notes, {link_count} links, {candidate_files} files with candidates to {VAULT_ROOT}")
    print(f"Report: {report_name}")


if __name__ == "__main__":
    main()
