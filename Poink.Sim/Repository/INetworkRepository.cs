// ── Brain Builder network storage ────────────────────────────────────────
//  Persists a named BrainBuilder config (encoders/architecture/hidden sizes/
//  actions -- the same BrainRecipe shape TrainedModelSummary carries for
//  reconstructing a trained brain) with no weights attached, so a
//  configuration can be saved, browsed, edited, and reloaded into the panel
//  while tuning, independently of ever having built or trained a brain from
//  it. Same "Poink.Sim" database as IModelRepository (see that interface's
//  doc comment), just another table.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Repository;

// SavedAt is when this network was first created, UpdatedAt when it (or its
// name) was last edited -- distinct the same way TrainedModelSummary
// distinguishes SavedAt from TrainedAt.
public sealed record NetworkSummary(long Id, string Name, BrainRecipe Recipe, DateTime SavedAt, DateTime UpdatedAt);

internal interface INetworkRepository
{
    long SaveNetwork(string name, BrainRecipe recipe);

    List<NetworkSummary> ListNetworks();

    void UpdateNetwork(long id, string name, BrainRecipe recipe);

    void DeleteNetwork(long id);
}
