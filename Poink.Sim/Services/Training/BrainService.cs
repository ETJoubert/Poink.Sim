
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Brain;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;

namespace Poink.Sim.Training;

public sealed record BrainOption(string Name, bool IsNeuralNetwork);

public sealed record NeuralNetworkSummary(Guid Id, string Name, BrainRecipe Recipe, DateTime CreatedAt, DateTime? UpdatedAt);

internal sealed class BrainService : IBrainService
{
    private static readonly JsonSerializerOptions StructureJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly INeuralNetworkService _neuralNetworkService;
    private readonly BrainRegistry _brainRegistry;
    private readonly ILogger<BrainService> _logger;

    private readonly Dictionary<string, BrainRecipe> _brainRecipes = new();
    private readonly HashSet<string> _draftBrainNames = new();

    public BrainService(INeuralNetworkService neuralNetworkService, BrainRegistry brainRegistry, ILogger<BrainService> logger)
    {
        _neuralNetworkService = neuralNetworkService;
        _brainRegistry = brainRegistry;
        _logger = logger;

        try
        {
            Brains = RunSync(() => _neuralNetworkService.ListAsync()).Select(ToSummary).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list brains at startup; continuing with none");
        }
    }

    // INeuralNetworkService is itself a singleton (backed by IDbContextFactory), so it's safe to
    // call directly -- Task.Run just bridges its async API onto BrainService's synchronous one.
    private static T RunSync<T>(Func<Task<T>> action) => Task.Run(action).GetAwaiter().GetResult();

    private static NeuralNetworkSummary ToSummary(NeuralNetworkModel model) =>
        new(
            model.Id, model.Name,
            JsonSerializer.Deserialize<BrainRecipe>(model.StructureJson, StructureJsonOptions) ?? new BrainRecipe([], ArchitectureKind.Mlp, []),
            model.CreatedAt, model.UpdatedAt);

    public IReadOnlyList<NeuralNetworkSummary> Brains { get; private set; } = [];

    public IReadOnlyList<string> BrainNames => _brainRegistry.All.Select(b => b.Name).ToList();

    public IReadOnlyList<string> DraftBrainNames => _draftBrainNames.ToList();

    public IReadOnlyList<BrainOption> BrainOptions =>
        _brainRegistry.All.Select(b => new BrainOption(b.Name, b is IDescribableBrain)).ToList();

    public string? Status { get; private set; }

    public event Action? OnChanged;
    public event Action<string>? BrainReset;

    public BrainRecipe? GetRecipe(string brainName) =>
        _brainRecipes.TryGetValue(brainName, out var recipe) ? recipe : Brains.FirstOrDefault(b => b.Name == brainName)?.Recipe;

    public IReadOnlyList<NetworkLayerShape> DescribeNetworkShape(string brainName) =>
        _brainRegistry.All.OfType<IPolicyGradientBrain>().FirstOrDefault(b => b.Name == brainName)?.DescribeNetworkShape() ?? [];

    public int GetWarmupBars(string brainName) => _brainRegistry.All.FirstOrDefault(b => b.Name == brainName) switch
    {
        IPolicyGradientBrain policyBrain => policyBrain.WarmupBars,
        INeatPolicyBrain neatBrain => neatBrain.WarmupBars,
        _ => 0,
    };


    private static IDescribableBrain BuildFromRecipe(string name, BrainRecipe recipe)
    {
        var parts = recipe.EncoderKinds.Select(k => BrainCatalog.CreateEncoder(k, recipe.EncoderSizes)).ToArray();
        var actionOutputHeads = (recipe.ActionOutputKinds ?? [])
            .Select(k => BrainCatalog.CreateActionOutputHead(k, recipe.ActionOutputEncodingKind, recipe.DecisionDecoding, recipe.DecisionTemperature)).ToList();
        var directions = recipe.EnabledDirections ?? ComposedPolicyBrain.AllDirections;

        if (recipe.ArchitectureKind == ArchitectureKind.Neat)
            return NeatPolicyBrain.Create(
                name, recipe.NeatHyperparameters ?? new NeatHyperparameters(), actionOutputHeads, directions,
                recipe.DecisionDecoding, recipe.DecisionTemperature, recipe.EnabledMaskRules, parts);

        var architecture = BrainCatalog.CreateArchitecture(recipe.ArchitectureKind, recipe.HiddenSizes);
        return ComposedPolicyBrain.Create(
            name, architecture, actionOutputHeads, directions, recipe.DecisionDecoding, recipe.DecisionTemperature, recipe.CnnKernelSweepSource,
            recipe.EnabledMaskRules, parts);
    }

    public IDescribableBrain BuildAndRegister(string name, BrainRecipe recipe)
    {
        var brain = BuildFromRecipe(name, recipe);
        _brainRegistry.Register(brain);
        _brainRecipes[name] = recipe;
        return brain;
    }


    private bool ValidateBuildRequest(string name, IReadOnlyList<EncoderKind> encoderKinds, IReadOnlyList<SimulationAction>? enabledDirections)
    {
        if (string.IsNullOrWhiteSpace(name))
        { Status = "enter a name for the new brain"; OnChanged?.Invoke(); return false; }
        if (encoderKinds.Count == 0)
        { Status = "pick at least one encoder"; OnChanged?.Invoke(); return false; }
        if (enabledDirections is { Count: 0 })
        { Status = "pick at least one action"; OnChanged?.Invoke(); return false; }
        if (_brainRegistry.All.Any(b => b.Name == name && b is not IDescribableBrain))
        { Status = $"'{name}' is already used by a built-in brain"; OnChanged?.Invoke(); return false; }
        return true;
    }

    public bool TryBuildDraftBrain(
      string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes,
      IReadOnlyList<ActionOutputKind>? actionOutputKinds = null, IReadOnlyList<SimulationAction>? enabledDirections = null,
      EncoderSizeOptions? encoderSizes = null, DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
      double decisionTemperature = 1.0, CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow,
      IReadOnlyList<DirectionMaskRuleKind>? enabledMaskRules = null, NeatHyperparameters? neatHyperparameters = null)
    {
        if (!ValidateBuildRequest(name, encoderKinds, enabledDirections))
            return false;

        var recipe = new BrainRecipe(
            encoderKinds.ToList(), architectureKind, hiddenSizes.ToList(),
            actionOutputKinds?.ToList(), ActionOutputEncodingKind.DiscretizedLevels, enabledDirections?.ToList(), encoderSizes,
            decisionDecoding, decisionTemperature, cnnKernelSweepSource, enabledMaskRules?.ToList(), neatHyperparameters);

        try
        {
            var brain = BuildFromRecipe(name, recipe);
            if (_brainRegistry.All.Any(b => b.Name == name))
            {
                _brainRegistry.Unregister(name);
                BrainReset?.Invoke(name);
            }
            _brainRegistry.Register(brain);
            _brainRecipes[name] = recipe;
            _draftBrainNames.Add(name);
            Status = null;
            OnChanged?.Invoke();
            _logger.LogInformation("Built draft brain (not saved): name={Name}, encoders={Encoders}, architecture={Architecture}",
                name, string.Join(",", encoderKinds), architectureKind);
            return true;
        }
        catch (Exception ex)
        {
            Status = $"couldn't build brain: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to build draft brain: name={Name}", name);
            return false;
        }
    }


    public bool TryBuildBrain(
        string name, IReadOnlyList<EncoderKind> encoderKinds, ArchitectureKind architectureKind, IReadOnlyList<int> hiddenSizes,
        IReadOnlyList<ActionOutputKind>? actionOutputKinds = null, IReadOnlyList<SimulationAction>? enabledDirections = null,
        EncoderSizeOptions? encoderSizes = null, DecisionDecodingKind decisionDecoding = DecisionDecodingKind.Softmax,
        double decisionTemperature = 1.0, CnnKernelSweepSource cnnKernelSweepSource = CnnKernelSweepSource.BarWindow,
        IReadOnlyList<DirectionMaskRuleKind>? enabledMaskRules = null, NeatHyperparameters? neatHyperparameters = null)
    {
        if (!ValidateBuildRequest(name, encoderKinds, enabledDirections))
            return false;

        var recipe = new BrainRecipe(
            encoderKinds.ToList(), architectureKind, hiddenSizes.ToList(),
            actionOutputKinds?.ToList(), ActionOutputEncodingKind.DiscretizedLevels, enabledDirections?.ToList(), encoderSizes,
            decisionDecoding, decisionTemperature, cnnKernelSweepSource, enabledMaskRules?.ToList(), neatHyperparameters);

        try
        {
            bool alreadyLive = _brainRegistry.All.Any(b => b.Name == name);
            if (!alreadyLive)
            {
                var brain = BuildFromRecipe(name, recipe);
                _brainRegistry.Register(brain);
                _logger.LogInformation("Built new brain: name={Name}, encoders={Encoders}, architecture={Architecture}",
                    name, string.Join(",", encoderKinds), architectureKind);
            }
            _brainRecipes[name] = recipe;
            _draftBrainNames.Remove(name);
            Status = null;

            var existing = Brains.FirstOrDefault(b => b.Name == name);
            bool persisted = existing is not null
                ? UpdateNetwork(existing.Id, name, recipe)
                : CreateNetwork(name, recipe);
            if (!persisted)
            {
                Status = $"built '{name}' live, but couldn't save it to storage: {Status}";
                OnChanged?.Invoke();
                _logger.LogWarning("Failed to persist built brain '{Name}' as a network: {Status}", name, Status);
                return false;
            }

            OnChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"couldn't build brain: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to build brain: name={Name}", name);
            return false;
        }
    }


    public bool CreateNetwork(string name, BrainRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "enter a name to save the network under";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            string structureJson = JsonSerializer.Serialize(recipe, StructureJsonOptions);
            RunSync(() => _neuralNetworkService.CreateAsync(name, structureJson));
            Status = $"saved '{name}'";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"save failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to save network '{Name}'", name);
            return false;
        }
    }

    public bool UpdateNetwork(Guid id, string name, BrainRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "enter a name to save the network under";
            OnChanged?.Invoke();
            return false;
        }

        try
        {
            string structureJson = JsonSerializer.Serialize(recipe, StructureJsonOptions);
            if (!RunSync(() => _neuralNetworkService.UpdateAsync(id, name, structureJson)))
            {
                Status = $"update failed: network id {id} not found";
                OnChanged?.Invoke();
                return false;
            }

            Status = $"updated '{name}'";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"update failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to update network id {Id}", id);
            return false;
        }
    }

    public bool DeleteNetwork(Guid id)
    {
        try
        {
            if (!RunSync(() => _neuralNetworkService.DeleteAsync(id)))
            {
                Status = "network not found";
                OnChanged?.Invoke();
                return false;
            }

            Status = "network deleted";
            Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Status = $"delete failed: {ex.Message}";
            OnChanged?.Invoke();
            _logger.LogWarning(ex, "Failed to delete network id {Id}", id);
            return false;
        }
    }

    public void Refresh()
    {
        try
        {
            Brains = RunSync(() => _neuralNetworkService.ListAsync()).Select(ToSummary).ToList();
        }
        catch (Exception ex)
        {
            Status = $"couldn't list brains: {ex.Message}";
            _logger.LogWarning(ex, "Failed to refresh brains list");
        }
        OnChanged?.Invoke();
    }
}
