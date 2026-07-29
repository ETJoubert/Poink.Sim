// ── Brain Builder preset storage ─────────────────────────────────────────
//  Persists a named BrainBuilder config (encoders/architecture/hidden sizes/
//  actions -- the same BrainRecipe shape SavedBrainSummary carries for
//  reconstructing a trained brain) with no weights attached, so a
//  configuration can be saved, browsed, edited, and reloaded into the panel
//  while tuning, independently of ever having built or trained a brain from
//  it. Same "Poink.Sim" database as IBrainRepository (see that interface's
//  doc comment), just a second table.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Repository;

// SavedAt is when this preset was first created, UpdatedAt when it (or its
// name) was last edited -- distinct the same way SavedBrainSummary
// distinguishes SavedAt from TrainedAt.
public sealed record BrainPresetSummary(long Id, string Name, BrainRecipe Recipe, DateTime SavedAt, DateTime UpdatedAt);

internal interface IBrainPresetRepository
{
    long SavePreset(string name, BrainRecipe recipe);

    List<BrainPresetSummary> ListPresets();

    void UpdatePreset(long id, string name, BrainRecipe recipe);

    void DeletePreset(long id);
}
